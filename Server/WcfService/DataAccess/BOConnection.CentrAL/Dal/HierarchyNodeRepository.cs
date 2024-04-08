﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;

using LSOmni.Common.Util;
using LSRetail.Omni.Domain.DataModel.Base;
using LSRetail.Omni.Domain.DataModel.Base.Hierarchies;
using LSRetail.Omni.Domain.DataModel.Base.Replication;

namespace LSOmni.DataAccess.BOConnection.CentrAL.Dal
{
    public class HierarchyNodeRepository : BaseRepository
    {
        // Key: Hierarchy Code
        const int TABLENODEID = 10000921;
        const int TABLELINKID = 10000922;

        private string sqlcolumnsNode = string.Empty;
        private string sqlfromNode = string.Empty;

        private string sqlcolumnsLink = string.Empty;
        private string sqlfromLink = string.Empty;

        public HierarchyNodeRepository(BOConfiguration config) : base(config)
        {
            sqlcolumnsNode = "mt.[Hierarchy Code],mt.[Node ID],mt.[Parent Node ID],mt.[Description],mt.[Children Order],mt.[Indentation],mt.[Presentation Order]," +
                             "(SELECT TOP(1) il.[Image Id] FROM [" + navCompanyName + "Retail Image Link$5ecfc871-5d82-43f1-9c54-59685e82318d] il " +
                             "WHERE il.[KeyValue]=(mt.[Hierarchy Code] + ',' + mt.[Node ID]) AND il.[TableName]='Hierar. Nodes' " +
                             "ORDER BY il.[Display Order]) AS ImageId";
            sqlfromNode = " FROM [" + navCompanyName + "Hierarchy Nodes$5ecfc871-5d82-43f1-9c54-59685e82318d] mt" +
                          " JOIN [" + navCompanyName + "Hierarchy Date$5ecfc871-5d82-43f1-9c54-59685e82318d] hd ON hd.[Hierarchy Code]=mt.[Hierarchy Code] AND hd.[Start Date]<=GETDATE()";

            sqlcolumnsLink = "mt.[Hierarchy Code],mt.[Node ID],mt.[Type],mt.[No_],mt.[Description]," +
                             "o.[Member Type],o.[Member Value],o.[Deal Price],o.[Validation Period ID],o.[Status]," +
                             "(SELECT TOP(1) il.[Image Id] FROM [" + navCompanyName + "Retail Image Link$5ecfc871-5d82-43f1-9c54-59685e82318d] il " +
                             "WHERE il.[KeyValue]=mt.[No_] AND il.[TableName]=CASE WHEN mt.[Type]=0 THEN 'Item' ELSE 'Offer' END " +
                             "ORDER BY il.[Display Order]) AS ImageId";
            sqlfromLink = " FROM [" + navCompanyName + "Hierarchy Node Link$5ecfc871-5d82-43f1-9c54-59685e82318d] mt" +
                          " JOIN [" + navCompanyName + "Hierarchy Date$5ecfc871-5d82-43f1-9c54-59685e82318d] hd ON hd.[Hierarchy Code]=mt.[Hierarchy Code] AND hd.[Start Date]<=GETDATE()" +
                          " LEFT JOIN [" + navCompanyName + "Offer$5ecfc871-5d82-43f1-9c54-59685e82318d] o ON o.[No_]=mt.[No_]";
        }

        public List<ReplHierarchyNode> ReplicateHierarchyNode(string storeId, int batchSize, bool fullReplication, ref string lastKey, ref string maxKey, ref int recordsRemaining)
        {
            if (string.IsNullOrWhiteSpace(lastKey))
                lastKey = "0";

            SQLHelper.CheckForSQLInjection(storeId);
            List<JscKey> keys = GetPrimaryKeys("Hierarchy Nodes$5ecfc871-5d82-43f1-9c54-59685e82318d");
            string where = string.Format(" AND hd.[Store Code]='{0}'", storeId);

            // get records remaining
            string sql = string.Empty;
            if (fullReplication)
            {
                sql = "SELECT COUNT(*)" + sqlfromNode + GetWhereStatement(true, keys, where, false);
            }
            recordsRemaining = GetRecordCount(TABLENODEID, lastKey, sql, keys, ref maxKey);

            List<JscActions> actions = LoadActions(fullReplication, TABLENODEID, batchSize, ref lastKey, ref recordsRemaining);
            List<ReplHierarchyNode> list = new List<ReplHierarchyNode>();

            // get records
            sql = GetSQL(fullReplication, batchSize) + sqlcolumnsNode + sqlfromNode + GetWhereStatement(fullReplication, keys, where, true);

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
                        TraceSqlCommand(command);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            int cnt = 0;
                            while (reader.Read())
                            {
                                list.Add(ReaderToHierarchyNode(reader, out lastKey));
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

                                list.Add(new ReplHierarchyNode()
                                {
                                    HierarchyCode = par[0],
                                    Id = par[1],
                                    IsDeleted = true
                                });
                                continue;
                            }

                            if (SetWhereValues(command, act, keys, first) == false)
                                continue;

                            TraceSqlCommand(command);
                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    list.Add(ReaderToHierarchyNode(reader, out string ts));
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

        public List<HierarchyNode> HierarchyNodeGet(string hCode, string storeId)
        {
            List<HierarchyNode> list = new List<HierarchyNode>();
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + sqlcolumnsNode + sqlfromNode + " AND hd.[Store Code]=@sid";
                    command.Parameters.AddWithValue("@sid", storeId);
                    if (string.IsNullOrEmpty(hCode) == false)
                    {
                        command.CommandText += " WHERE mt.[Hierarchy Code]=@id";
                        command.Parameters.AddWithValue("@id", hCode);
                    }
                    command.CommandText += " ORDER BY mt.[Presentation Order]";

                    TraceSqlCommand(command);
                    connection.Open();
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        HierarchyHospLeafRepository rep = new HierarchyHospLeafRepository(config, NavVersion);

                        while (reader.Read())
                        {
                            HierarchyNode rec = ReaderToHierarchyNode(reader, storeId);
                            rec.Leafs = HierarchyNodeLinkGet(rec.HierarchyCode, rec.Id, storeId);
                            rec.Nodes = HierarchyDealNodeGet(rec.HierarchyCode, rec.Id, storeId);
                            foreach (HierarchyNode node in rec.Nodes)
                            {
                                node.Nodes = rep.HierarchyDealGet(node.HierarchyCode, node.ParentNode, node.Id);
                                node.Leafs = rep.HierarchyDealLeafGet(node.HierarchyCode, node.ParentNode, node.Id);
                            }
                            list.Add(rec);
                        }
                    }
                    connection.Close();
                }
            }
            return list;
        }

        public List<ReplHierarchyLeaf> ReplicateHierarchyLeaf(string storeId, int batchSize, bool fullReplication, ref string lastKey, ref string maxKey, ref int recordsRemaining)
        {
            if (string.IsNullOrWhiteSpace(lastKey))
                lastKey = "0";

            SQLHelper.CheckForSQLInjection(storeId);
            List<JscKey> keys = GetPrimaryKeys("Hierarchy Node Link$5ecfc871-5d82-43f1-9c54-59685e82318d");
            string where = string.Format(" AND hd.[Store Code]='{0}'", storeId);

            // get records remaining
            string sql = string.Empty;
            if (fullReplication)
            {
                sql = "SELECT COUNT(*)" + sqlfromLink + GetWhereStatement(true, keys, where, false);
            }
            recordsRemaining = GetRecordCount(TABLELINKID, lastKey, sql, keys, ref maxKey);

            List<JscActions> actions = LoadActions(fullReplication, TABLELINKID, batchSize, ref lastKey, ref recordsRemaining);

            List<ReplHierarchyLeaf> list = new List<ReplHierarchyLeaf>();

            // get records
            sql = GetSQL(fullReplication, batchSize) + sqlcolumnsLink + sqlfromLink + GetWhereStatement(fullReplication, keys, where, true);

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
                        TraceSqlCommand(command);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            int cnt = 0;
                            while (reader.Read())
                            {
                                list.Add(ReaderToHierarchyNodeLink(reader, out lastKey));
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
                                if (par.Length < 4 || par.Length != keys.Count)
                                    continue;

                                list.Add(new ReplHierarchyLeaf()
                                {
                                    HierarchyCode = par[0],
                                    NodeId = par[1],
                                    Type = (HierarchyLeafType)Convert.ToInt32(par[2]),
                                    Id = par[3],
                                    IsDeleted = true
                                });
                                continue;
                            }

                            if (SetWhereValues(command, act, keys, first) == false)
                                continue;

                            TraceSqlCommand(command);
                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    list.Add(ReaderToHierarchyNodeLink(reader, out string ts));
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

        public List<HierarchyLeaf> HierarchyNodeLinkGet(string hCode, string nCode, string storeId)
        {
            List<HierarchyLeaf> list = new List<HierarchyLeaf>();
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + sqlcolumnsLink + sqlfromLink +
                                          " WHERE mt.[Hierarchy Code]=@Hid AND mt.[Node ID]=@Nid AND mt.[Type]=0 AND hd.[Store Code]=@sid";

                    command.Parameters.AddWithValue("@Hid", hCode);
                    command.Parameters.AddWithValue("@Nid", nCode);
                    command.Parameters.AddWithValue("@sid", storeId);
                    connection.Open();
                    TraceSqlCommand(command);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(ReaderToHierarchyNodeLink(reader));
                        }
                    }
                    connection.Close();
                }
            }
            return list;
        }

        public List<HierarchyNode> HierarchyDealNodeGet(string hCode, string nCode, string storeId)
        {
            List<HierarchyNode> list = new List<HierarchyNode>();
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + sqlcolumnsLink + sqlfromLink +
                                          " WHERE mt.[Hierarchy Code]=@Hid AND mt.[Node ID]=@Nid AND mt.[Type]=1 AND hd.[Store Code]=@sid";

                    command.Parameters.AddWithValue("@Hid", hCode);
                    command.Parameters.AddWithValue("@Nid", nCode);
                    command.Parameters.AddWithValue("@sid", storeId);
                    connection.Open();
                    TraceSqlCommand(command);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(ReaderToHierarchyDealNode(reader));
                        }
                    }
                    connection.Close();
                }
            }
            return list;
        }

        private ReplHierarchyNode ReaderToHierarchyNode(SqlDataReader reader, out string timestamp)
        {
            timestamp = ConvertTo.ByteArrayToString(reader["timestamp"] as byte[]);

            return new ReplHierarchyNode()
            {
                HierarchyCode = SQLHelper.GetString(reader["Hierarchy Code"]),
                Id = SQLHelper.GetString(reader["Node ID"]),
                ParentNode = SQLHelper.GetString(reader["Parent Node ID"]),
                Description = SQLHelper.GetString(reader["Description"]),
                ChildrenOrder = SQLHelper.GetInt32(reader["Children Order"]),
                Indentation = SQLHelper.GetInt32(reader["Indentation"]),
                PresentationOrder = SQLHelper.GetInt32(reader["Presentation Order"]),
                ImageId = SQLHelper.GetString(reader["ImageId"])
            };
        }

        private ReplHierarchyLeaf ReaderToHierarchyNodeLink(SqlDataReader reader, out string timestamp)
        {
            timestamp = ConvertTo.ByteArrayToString(reader["timestamp"] as byte[]);

            return new ReplHierarchyLeaf()
            {
                HierarchyCode = SQLHelper.GetString(reader["Hierarchy Code"]),
                NodeId = SQLHelper.GetString(reader["Node ID"]),
                Id = SQLHelper.GetString(reader["No_"]),
                Description = SQLHelper.GetString(reader["Description"]),
                Type = (HierarchyLeafType)SQLHelper.GetInt32(reader["Type"]),
                ImageId = SQLHelper.GetString(reader["ImageId"]),
                IsMemberClub = SQLHelper.GetBool(reader["Member Type"]),
                MemberValue = SQLHelper.GetString(reader["Member Value"]),
                DealPrice = SQLHelper.GetDecimal(reader, "Deal Price"),
                ValidationPeriod = SQLHelper.GetString(reader["Validation Period ID"]),
                IsActive = SQLHelper.GetBool(reader["Status"])
            };
        }

        private HierarchyNode ReaderToHierarchyNode(SqlDataReader reader, string storeId)
        {
            return new HierarchyNode()
            {
                HierarchyCode = SQLHelper.GetString(reader["Hierarchy Code"]),
                ParentNode = SQLHelper.GetString(reader["Parent Node ID"]),
                Id = SQLHelper.GetString(reader["Node ID"]),
                Description = SQLHelper.GetString(reader["Description"]),
                ImageId = SQLHelper.GetString(reader["ImageId"]),
                Indentation = SQLHelper.GetInt32(reader["Indentation"]),
                ChildrenOrder = SQLHelper.GetInt32(reader["Children Order"]),
                PresentationOrder = SQLHelper.GetInt32(reader["Presentation Order"])
            };
        }

        private HierarchyLeaf ReaderToHierarchyNodeLink(SqlDataReader reader)
        {
            HierarchyLeaf leaf = new HierarchyLeaf()
            {
                HierarchyCode = SQLHelper.GetString(reader["Hierarchy Code"]),
                ParentNode = SQLHelper.GetString(reader["Node ID"]),
                Id = SQLHelper.GetString(reader["No_"]),
                Description = SQLHelper.GetString(reader["Description"]),
                Type = (HierarchyLeafType)SQLHelper.GetInt32(reader["Type"]),
                ImageId = SQLHelper.GetString(reader["ImageId"])
            };

            ItemModifierRepository mrep = new ItemModifierRepository(config);
            leaf.Modifiers = mrep.ModifierGetByItemId(leaf.Id);

            ItemRecipeRepository rrep = new ItemRecipeRepository(config);
            leaf.Recipies = rrep.RecipeGetByItemId(leaf.Id);
            return leaf;
        }

        private HierarchyNode ReaderToHierarchyDealNode(SqlDataReader reader)
        {
            return new HierarchyNode()
            {
                HierarchyCode = SQLHelper.GetString(reader["Hierarchy Code"]),
                ParentNode = SQLHelper.GetString(reader["Node ID"]),
                Id = SQLHelper.GetString(reader["No_"]),
                Description = SQLHelper.GetString(reader["Description"]),
                Type = HierarchyDealType.Deal,
                ImageId = SQLHelper.GetString(reader["ImageId"])
            };
        }
    }
}
