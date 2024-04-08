﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using LSOmni.Common.Util;
using LSRetail.Omni.Domain.DataModel.Base;
using LSRetail.Omni.Domain.DataModel.Loyalty.OrderHosp;
using LSRetail.Omni.Domain.DataModel.Loyalty.Replication;

namespace LSOmni.DataAccess.BOConnection.NavSQL.Dal
{
    public class InvStatusRepository : BaseRepository
    {
        public InvStatusRepository(BOConfiguration config, Version navVersion) : base(config, navVersion)
        {
        }

        public virtual List<ReplInvStatus> ReplicateInventoryStatus(string storeId, int batchSize, bool fullReplication, ref string lastKey, ref string maxKey, ref int recordsRemaining)
        {
            if (NavVersion > new Version("14.0"))
                return ReplicateWithCounter(storeId, batchSize, fullReplication, ref lastKey, ref maxKey, ref recordsRemaining);

            return ReplicateNoCounter(storeId, batchSize, ref lastKey, ref maxKey, ref recordsRemaining);
        }

        public virtual List<ReplInvStatus> ReplicateWithCounter(string storeId, int batchSize, bool fullReplication, ref string lastKey, ref string maxKey, ref int recordsRemaining)
        {
            string sqlcolumns = "mt.[timestamp],mt.[Item No_],mt.[Variant Code],mt.[Store No_],mt.[Net Inventory],mt.[Replication Counter]";
            string sqlfrom = " FROM [" + navCompanyName + "Inventory Lookup Table] mt";
            string sqlwhere = string.IsNullOrEmpty(storeId) ? string.Empty : " WHERE mt.[Store No_]=@sid AND " + 
                ((fullReplication) ? "mt.[timestamp]>@cnt" : "mt.[Replication Counter]>@cnt");

            if (string.IsNullOrWhiteSpace(lastKey))
                lastKey = "0";

            List<ReplInvStatus> list = new List<ReplInvStatus>();
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand command = connection.CreateCommand())
                {
                    connection.Open();

                    // get records remaining
                    command.CommandText = "SELECT COUNT(*)" + sqlfrom + sqlwhere;
                    command.Parameters.AddWithValue("@sid", storeId);
                    if (fullReplication)
                    {
                        SqlParameter par = new SqlParameter("@cnt", SqlDbType.Timestamp);
                        if (lastKey == "0")
                            par.Value = new byte[] { 0 };
                        else
                            par.Value = StringToByteArray(lastKey);
                        command.Parameters.Add(par);
                    }
                    else
                    {
                        command.Parameters.AddWithValue("@cnt", lastKey);
                    }
                    TraceSqlCommand(command);
                    recordsRemaining = (int)command.ExecuteScalar();

                    if (string.IsNullOrEmpty(maxKey) || maxKey == "0")
                    {
                        // get max value
                        command.CommandText = "SELECT MAX([Replication Counter])" + sqlfrom + sqlwhere;
                        var ret = command.ExecuteScalar();
                        maxKey = (ret == DBNull.Value) ? "0" : ret.ToString();
                    }

                    // get data
                    command.CommandText = "SELECT " + ((batchSize > 0) ? "TOP(" + batchSize.ToString() + ") " : string.Empty) + sqlcolumns + sqlfrom + sqlwhere + " ORDER BY " +
                        ((fullReplication) ? "mt.[timestamp]" : "mt.[Replication Counter]");

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        int cnt = 0;
                        while (reader.Read())
                        {
                            if (fullReplication)
                                lastKey = ConvertTo.ByteArrayToString(reader["timestamp"] as byte[]);
                            else
                                lastKey = SQLHelper.GetString(reader["Replication Counter"]);

                            list.Add(new ReplInvStatus()
                            {
                                ItemId = SQLHelper.GetString(reader["Item No_"]),
                                VariantId = SQLHelper.GetString(reader["Variant Code"]),
                                StoreId = SQLHelper.GetString(reader["Store No_"]),
                                Quantity = SQLHelper.GetDecimal(reader, "Net Inventory"),
                                IsDeleted = false
                            });
                            cnt++;
                        }
                        reader.Close();
                        recordsRemaining -= cnt;
                    }

                    if (recordsRemaining <= 0)
                        lastKey = maxKey;   // this should be the highest PreAction id;

                    connection.Close();
                }
            }

            // just in case something goes too far
            if (recordsRemaining < 0)
                recordsRemaining = 0;

            return list;
        }

        public virtual List<ReplInvStatus> ReplicateNoCounter(string storeId, int batchSize, ref string lastKey, ref string maxKey, ref int recordsRemaining)
        {
            string sqlcolumns = "mt.[timestamp],mt.[Item No_],mt.[Variant Code],mt.[Store No_],mt.[Net Inventory]";
            string sqlfrom = " FROM [" + navCompanyName + "Inventory Lookup Table] mt";

            SQLHelper.CheckForSQLInjection(storeId);
            string sqlwhere = (string.IsNullOrEmpty(storeId) ? string.Empty : string.Format(" AND mt.[Store No_]='{0}'", storeId));

            if (string.IsNullOrWhiteSpace(lastKey))
                lastKey = "0";

            List<JscKey> keys = GetPrimaryKeys("Inventory Lookup Table");

            // get records remaining
            string sql = string.Empty;
            sql = "SELECT COUNT(*)";
            if (batchSize > 0)
            {
                sql += sqlfrom + GetWhereStatement(true, keys, sqlwhere, false);
            }
            recordsRemaining = GetRecordCount(99001608, lastKey, sql, (batchSize > 0) ? keys : null, ref maxKey);

            List<ReplInvStatus> list = new List<ReplInvStatus>();

            // get records
            sql = GetSQL(true, batchSize) + sqlcolumns + sqlfrom + GetWhereStatement(true, keys, sqlwhere, true);

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand command = connection.CreateCommand())
                {
                    connection.Open();
                    command.CommandText = sql;

                    JscActions act = new JscActions(lastKey);
                    SetWhereValues(command, act, keys, true, true);
                    TraceSqlCommand(command);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        int cnt = 0;
                        while (reader.Read())
                        {
                            lastKey = ConvertTo.ByteArrayToString(reader["timestamp"] as byte[]);

                            list.Add(new ReplInvStatus()
                            {
                                ItemId = SQLHelper.GetString(reader["Item No_"]),
                                VariantId = SQLHelper.GetString(reader["Variant Code"]),
                                StoreId = SQLHelper.GetString(reader["Store No_"]),
                                Quantity = SQLHelper.GetDecimal(reader, "Net Inventory"),
                                IsDeleted = false
                            });
                            cnt++;
                        }
                        reader.Close();
                        recordsRemaining -= cnt;
                    }
                    if (recordsRemaining <= 0)
                        lastKey = maxKey;   // this should be the highest PreAction id;
                    connection.Close();
                }
            }

            // just in case something goes too far
            if (recordsRemaining < 0)
                recordsRemaining = 0;

            return list;
        }

        public virtual List<HospAvailabilityResponse> CheckAvailability(List<HospAvailabilityRequest> request, string storeId)
        {
            List<HospAvailabilityResponse> list = new List<HospAvailabilityResponse>();
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand command = connection.CreateCommand())
                {
                    string sql = "SELECT mt.[Store No_],mt.[Type],mt.[No_],mt.[Available Qty_],mt.[Unit of Measure] " +
                                 "FROM [" + navCompanyName + "Current Availability] mt";

                    connection.Open();
                    if (request == null || request.Count == 0)
                    {
                        command.CommandText = sql;
                        if (string.IsNullOrEmpty(storeId) == false)
                        {
                            command.CommandText += " WHERE mt.[Store No_]=@sid";
                            command.Parameters.AddWithValue("@sid", storeId);
                        }

                        TraceSqlCommand(command);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                list.Add(ReaderToHospResponse(reader));
                            }
                            reader.Close();
                        }
                    }
                    else
                    {
                        foreach (HospAvailabilityRequest req in request)
                        {
                            command.Parameters.Clear();

                            command.CommandText = sql + " WHERE mt.[No_]=@no";
                            command.Parameters.AddWithValue("@no", req.ItemId);

                            if (string.IsNullOrEmpty(req.UnitOfMeasure) == false)
                            {
                                command.CommandText += " AND mt.[Unit of Measure]=@uom";
                                command.Parameters.AddWithValue("@uom", req.UnitOfMeasure);
                            }

                            if (string.IsNullOrEmpty(storeId) == false)
                            {
                                command.CommandText += " AND mt.[Store No_]=@sid";
                                command.Parameters.AddWithValue("@sid", storeId);
                            }

                            TraceSqlCommand(command);
                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    list.Add(ReaderToHospResponse(reader));
                                }
                                reader.Close();
                            }
                        }
                    }
                    connection.Close();
                }
            }
            return list;
        }

        private HospAvailabilityResponse ReaderToHospResponse(SqlDataReader reader)
        {
            return new HospAvailabilityResponse()
            {
                Number = SQLHelper.GetString(reader["No_"]),
                UnitOfMeasure = SQLHelper.GetString(reader["Unit of Measure"]),
                StoreId = SQLHelper.GetString(reader["Store No_"]),
                IsDeal = SQLHelper.GetBool(reader["Type"]),
                Quantity = SQLHelper.GetDecimal(reader, "Available Qty_")
            };
        }
    }
}
