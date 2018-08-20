﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;

using LSOmni.Common.Util;
using LSRetail.Omni.Domain.DataModel.Loyalty.Items;
using LSRetail.Omni.Domain.DataModel.Loyalty.Replication;

namespace LSOmni.DataAccess.BOConnection.NavSQL.Dal
{
    public class AttributeOptionValueRepository : BaseRepository
    {
        // Key: Attribute Code,Sequence
        const int TABLEID = 10000785;

        private string sqlcolumns = string.Empty;
        private string sqlfrom = string.Empty;

        public AttributeOptionValueRepository() : base()
        {
            sqlcolumns = "mt.[Attribute Code],mt.[Sequence],mt.[Option Value]";

            sqlfrom = " FROM [" + navCompanyName + "Attribute Option Value] mt";
        }

        public List<ReplAttributeOptionValue> ReplicateEcommAttributeOptionValue(int batchSize, bool fullReplication, ref string lastKey, ref string maxKey, ref int recordsRemaining)
        {
            if (string.IsNullOrWhiteSpace(lastKey))
                lastKey = "0";

            List<JscKey> keys = GetPrimaryKeys("Attribute Option Value");

            // get records remaining
            string sql = string.Empty;
            if (fullReplication)
            {
                sql = "SELECT COUNT(*)";
                if (batchSize > 0)
                {
                    sql += sqlfrom + GetWhereStatement(true, keys, false);
                }
            }
            recordsRemaining = GetRecordCount(TABLEID, lastKey, sql, (batchSize > 0) ? keys : null, ref maxKey);

            List<JscActions> actions = LoadActions(fullReplication, TABLEID, batchSize, ref lastKey, ref recordsRemaining);
            List<ReplAttributeOptionValue> list = new List<ReplAttributeOptionValue>();

            // get records
            sql = GetSQL(fullReplication, batchSize) + sqlcolumns + sqlfrom + GetWhereStatement(fullReplication, keys, true);

            TraceIt(sql);
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand command = connection.CreateCommand())
                {
                    connection.Open();
                    command.CommandText = sql;

                    if (fullReplication)
                    {
                        JscActions act = new JscActions(lastKey);
                        SetWhereValues(command, act, keys, true, true);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            int cnt = 0;
                            while (reader.Read())
                            {
                                list.Add(ReaderToEcommAttributeOptionValue(reader, out lastKey));
                                cnt++;
                            }
                            reader.Close();
                            recordsRemaining -= cnt;
                        }
                        if (recordsRemaining <= 0)
                            lastKey = maxKey;   // this should be the highest PreAction id;
                    }
                    else
                    {
                        bool first = true;
                        foreach (JscActions act in actions)
                        {
                            if (act.Type == DDStatementType.Delete)
                            {
                                string[] par = act.ParamValue.Split(';');
                                if (par.Length < 2 || par.Length != keys.Count)
                                    continue;

                                list.Add(new ReplAttributeOptionValue()
                                {
                                    Code = par[0],
                                    Sequence = Convert.ToInt32(par[1]),
                                    IsDeleted = true
                                });
                                continue;
                            }

                            if (SetWhereValues(command, act, keys, first) == false)
                                continue;

                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    list.Add(ReaderToEcommAttributeOptionValue(reader, out string ts));
                                }
                                reader.Close();
                            }
                            first = false;
                        }
                        if (string.IsNullOrEmpty(maxKey))
                            maxKey = lastKey;
                    }
                    connection.Close();
                }
            }

            // just in case something goes too far
            if (recordsRemaining < 0)
                recordsRemaining = 0;

            return list;
        }

        public List<AttributeOptionValue> GetOptionValues(string id)
        {
            List<AttributeOptionValue> list = new List<AttributeOptionValue>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand command = connection.CreateCommand())
                {
                    connection.Open();

                    command.CommandText = "SELECT " + sqlcolumns + sqlfrom + " WHERE mt.[Attribute Code]=@id ORDER BY mt.[Sequence]";
                    command.Parameters.AddWithValue("@id", id);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(ReaderToEcommAttributeOptionValue(reader));
                        }
                        reader.Close();
                    }
                    connection.Close();
                }
            }
            return list;
        }

        private AttributeOptionValue ReaderToEcommAttributeOptionValue(SqlDataReader reader)
        {
            return new AttributeOptionValue()
            {
                Code = SQLHelper.GetString(reader["Attribute Code"]),
                Sequence = SQLHelper.GetInt32(reader["Sequence"]),
                Value = SQLHelper.GetString(reader["Option Value"])
            };
        }

        private ReplAttributeOptionValue ReaderToEcommAttributeOptionValue(SqlDataReader reader, out string timestamp)
        {
            timestamp = ByteArrayToString(reader["timestamp"] as byte[]);

            return new ReplAttributeOptionValue()
            {
                Code = SQLHelper.GetString(reader["Attribute Code"]),
                Sequence = SQLHelper.GetInt32(reader["Sequence"]),
                Value = SQLHelper.GetString(reader["Option Value"])
            };
        }
    }
}