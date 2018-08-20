﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;

using LSOmni.Common.Util;
using LSRetail.Omni.Domain.DataModel.Base.Retail;
using LSRetail.Omni.Domain.DataModel.Base.Replication;

namespace LSOmni.DataAccess.BOConnection.NavSQL.Dal
{
    public class ImageRepository : BaseRepository
    {
        private string sqlimgfrom = string.Empty;
        private string sqlimgfields = string.Empty;

        private string sqllinkfrom = string.Empty;
        private string sqllinkfields = string.Empty;

        const int IMAGE_TABLEID = 99009063;
        const int LINK_TABLEID = 99009064;

        public ImageRepository() : base()
        {
            sqlimgfields = "mt.[Code],mt.[Type],mt.[Image Location],mt.[Image Blob],mt.[Description]";
            sqlimgfrom = " FROM [" + navCompanyName + "Retail Image] mt";

            sqllinkfields = "mt.[Image Id],mt.[Display Order],mt.[TableName],mt.[KeyValue],mt.[Description]";
            sqllinkfrom = " FROM [" + navCompanyName + "Retail Image Link] mt";
        }


        public ImageView ImageGetById(string id)
        {
            ImageView view = null;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT mt.[Code] as [Image Id], 0 as [Display Order], mt.[Type],mt.[Image Location],mt.[Image Blob]" + sqlimgfrom +
                                         " WHERE mt.[Code]=@id";     //must return lowest displayorder first
                    command.Parameters.AddWithValue("@id", id);
                    TraceSqlCommand(command);
                    connection.Open();
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            view = ReaderToImage(reader, true);
                        }
                        reader.Close();
                    }
                    connection.Close();
                }
            }
            return view;
        }

        public List<ImageView> ImageGetByKey(string tableName, string key1, string key2, string key3, int imgCount, bool includeBlob)
        {
            try
            {
                List<ImageView> list = new List<ImageView>();
                string sqlcnt = string.Empty;
                if (imgCount > 0)
                    sqlcnt = " TOP(" + imgCount.ToString() + ") ";

                string sql = "SELECT " + sqlcnt + "mt.[Type],mt.[Image Location],il.[Image Id],il.[Display Order]" +
                            ((includeBlob) ? ",mt.[Image Blob]" : string.Empty) +
                             sqlimgfrom + " INNER JOIN [" + navCompanyName + "Retail Image Link] il ON mt.[Code]=il.[Image Id]" +
                             " WHERE il.[KeyValue]=@key AND il.[TableName]=@table " +
                             " ORDER BY il.[Display Order]";

                string keyvalue = key1;
                if (string.IsNullOrWhiteSpace(key2) == false)
                    keyvalue += "," + key2;
                if (string.IsNullOrWhiteSpace(key3) == false)
                    keyvalue += "," + key3;

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    using (SqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = sql;
                        command.Parameters.AddWithValue("@key", keyvalue);
                        command.Parameters.AddWithValue("@table", tableName);
                        TraceSqlCommand(command);
                        connection.Open();
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                list.Add(ReaderToImage(reader, includeBlob));
                            }
                            reader.Close();
                        }
                        connection.Close();
                    }
                }
                return list;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private ImageView ReaderToImage(SqlDataReader reader, bool includeblob)
        {
            ImageView view = new ImageView()
            {
                Id = SQLHelper.GetString(reader["Image Id"]),
                DisplayOrder = SQLHelper.GetInt32(reader["Display Order"]),
                Location = SQLHelper.GetString(reader["Image Location"]),
                LocationType = (LocationType)SQLHelper.GetInt32(reader["Type"])
            };

            if (includeblob)
                view.ImgBytes = ImageConverter.NAVUnCompressImage(reader["Image Blob"] as byte[]);
            return view;
        }

        public List<ReplImage> ReplEcommImage(int batchSize, bool fullReplication, ref string lastKey, ref string maxKey, ref int recordsRemaining)
        {
            if (string.IsNullOrWhiteSpace(lastKey))
                lastKey = "0";

            List<JscKey> keys = GetPrimaryKeys("Retail Image");

            // get records remaining
            string sql = string.Empty;
            if (fullReplication)
            {
                sql = "SELECT COUNT(*) ";
                if (batchSize > 0)
                {
                    sql += sqlimgfrom + GetWhereStatement(true, keys, false);
                }
            }
            recordsRemaining = GetRecordCount(IMAGE_TABLEID, lastKey, sql, (batchSize > 0) ? keys : null, ref maxKey);

            List<JscActions> actions = LoadActions(fullReplication, IMAGE_TABLEID, batchSize, ref lastKey, ref recordsRemaining);
            List<ReplImage> list = new List<ReplImage>();

            // get records
            sql = GetSQL(fullReplication, batchSize) + sqlimgfields + sqlimgfrom + GetWhereStatement(fullReplication, keys, true);

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
                                list.Add(ReaderToImage(reader, out lastKey));
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
                                list.Add(new ReplImage()
                                {
                                    Id = act.ParamValue,
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
                                    list.Add(ReaderToImage(reader, out string ts));
                                }
                                reader.Close();
                            }
                            first = false;
                        }
                    }
                    connection.Close();
                }
            }

            // just in case something goes too far
            if (recordsRemaining < 0)
                recordsRemaining = 0;

            return list;
        }

        private ReplImage ReaderToImage(SqlDataReader reader, out string timestamp)
        {
            ReplImage img = new ReplImage()
            {
                Id = SQLHelper.GetString(reader["Code"]),
                Location = SQLHelper.GetString(reader["Image Location"]),
                LocationType = (LocationType)SQLHelper.GetInt32(reader["Type"]),
                Description = SQLHelper.GetString(reader["Description"])
            };

            byte[] imgbyte = SQLHelper.GetByteArray(reader["Image Blob"]);
            if (imgbyte == null)
                img.Image64 = string.Empty;
            else
                img.Image64 = Convert.ToBase64String(imgbyte);

            timestamp = ByteArrayToString(reader["timestamp"] as byte[]);
            return img;
        }

        public List<ReplImageLink> ReplEcommImageLink(int batchSize, bool fullReplication, ref string lastKey, ref string maxKey, ref int recordsRemaining)
        {
            if (string.IsNullOrWhiteSpace(lastKey))
                lastKey = "0";

            List<JscKey> keys = GetPrimaryKeys("Retail Image Link");

            // get records remaining
            string sql = string.Empty;
            if (fullReplication)
            {
                sql = "SELECT COUNT(*) ";
                if (batchSize > 0)
                {
                    sql += sqllinkfrom + GetWhereStatement(true, keys, false);
                }
            }
            recordsRemaining = GetRecordCount(LINK_TABLEID, lastKey, sql, (batchSize > 0) ? keys : null, ref maxKey);

            List<JscActions> actions = LoadActions(fullReplication, LINK_TABLEID, batchSize, ref lastKey, ref recordsRemaining);
            List<ReplImageLink> list = new List<ReplImageLink>();

            // get records
            sql = GetSQL(fullReplication, batchSize) + sqllinkfields + sqllinkfrom + GetWhereStatement(fullReplication, keys, true);

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
                                list.Add(ReaderToImageLink(reader, out lastKey));
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
                                list.Add(new ReplImageLink()
                                {
                                    ImageId = act.ParamValue,
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
                                    list.Add(ReaderToImageLink(reader, out string ts));
                                }
                                reader.Close();
                            }
                            first = false;
                        }
                    }
                    connection.Close();
                }
            }

            // just in case something goes too far
            if (recordsRemaining < 0)
                recordsRemaining = 0;

            return list;
        }

        private ReplImageLink ReaderToImageLink(SqlDataReader reader, out string timestamp)
        {
            timestamp = ByteArrayToString(reader["timestamp"] as byte[]);

            return new ReplImageLink
            {
                DisplayOrder = SQLHelper.GetInt32(reader["Display Order"]),
                ImageId = SQLHelper.GetString(reader["Image Id"]),
                KeyValue = SQLHelper.GetString(reader["KeyValue"]),
                TableName = SQLHelper.GetString(reader["TableName"]),
                Description = SQLHelper.GetString(reader["Description"])
            };
        }
    }
}
 