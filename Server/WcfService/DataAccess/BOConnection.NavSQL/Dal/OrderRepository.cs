﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;

using LSOmni.Common.Util;
using LSRetail.Omni.Domain.DataModel.Base.Retail;
using LSRetail.Omni.Domain.DataModel.Base.SalesEntries;
using LSRetail.Omni.Domain.DataModel.Base;

namespace LSOmni.DataAccess.BOConnection.NavSQL.Dal
{
    public class OrderRepository : BaseRepository
    {
        private string sql = string.Empty;
        private string documentId = string.Empty;
        private string cardCustNo = string.Empty;

        public OrderRepository(BOConfiguration config, Version navVersion) : base(config, navVersion)
        {
            cardCustNo = (navVersion > new Version("14.2")) ? "Card or Customer No_" : "Card or Customer number";

            if (navVersion > new Version("13.5"))
            {
                string date = (navVersion > new Version("14.2")) ? "Created" : "Document DateTime";
                string full = (navVersion > new Version("14.2")) ? "Name" : "Full Name";

                documentId = "Document ID";
                sql = "SELECT * FROM (" +
                        "SELECT mt.[Document ID],mt.[Store No_],mt.[External ID],mt.[" + date + "] AS Date,mt.[Source Type]," +
                        "mt.[Member Card No_],mt.[" + full + "] AS Name,mt.[Address],mt.[Address 2]," +
                        "mt.[City],mt.[County],mt.[Post Code],mt.[Country_Region Code],mt.[Phone No_],mt.[Email],mt.[House_Apartment No_]," +
                        "mt.[Mobile Phone No_],mt.[Daytime Phone No_],mt.[Ship-to " + full + "] AS ShipName,mt.[Ship-to Address],mt.[Ship-to Address 2]," +
                        "mt.[Ship-to City],mt.[Ship-to County],mt.[Ship-to Post Code],mt.[Ship-to Country_Region Code],mt.[Ship-to Phone No_]," +
                        "mt.[Ship-to Email],mt.[Ship-to House_Apartment No_],mt.[Click and Collect Order], mt.[Shipping Agent Code]," +
                        "mt.[Shipping Agent Service Code], 0 AS Posted," +
                        "(SELECT COUNT(*) FROM [" + navCompanyName + "Customer Order Payment] cop WHERE cop.[Document ID]=mt.[Document ID]) AS CoPay " +
                        "FROM [" + navCompanyName + "Customer Order Header] mt " +
                        "UNION " +
                        "SELECT mt.[Document ID],mt.[Store No_],mt.[External ID],mt.[" + date + "] AS Date,mt.[Source Type]," +
                        "mt.[Member Card No_],mt.[" + full + "] AS Name,mt.[Address],mt.[Address 2]," +
                        "mt.[City],mt.[County],mt.[Post Code],mt.[Country_Region Code],mt.[Phone No_],mt.[Email],mt.[House_Apartment No_]," +
                        "mt.[Mobile Phone No_],mt.[Daytime Phone No_],mt.[Ship-to " + full + "] AS ShipName,mt.[Ship-to Address],mt.[Ship-to Address 2]," +
                        "mt.[Ship-to City],mt.[Ship-to County],mt.[Ship-to Post Code],mt.[Ship-to Country_Region Code],mt.[Ship-to Phone No_]," +
                        "mt.[Ship-to Email],mt.[Ship-to House_Apartment No_],mt.[Click and Collect Order], mt.[Shipping Agent Code]," +
                        "mt.[Shipping Agent Service Code],1 AS Posted," +
                        "(SELECT COUNT(*) FROM [" + navCompanyName + "Posted Customer Order Payment] cop WHERE cop.[Document ID]=mt.[Document ID]) AS CoPay " +
                        "FROM [" + navCompanyName + "Posted Customer Order Header] mt) AS Orders";
            }
            else
            {
                documentId = "Document Id";
                sql = "SELECT * FROM (" +
                        "SELECT mt.[Document Id],mt.[Store No_],mt.[Web Trans_ GUID],mt.[Document DateTime] AS Date,mt.[Source Type]," +
                        "mt.[Member Card No_],mt.[Member Contact No_],mt.[Member Contact Name]," +
                        "mt.[Full Name],mt.[Address],mt.[Address 2],mt.[City],mt.[County],mt.[Post Code],mt.[Country Region Code]," +
                        "mt.[Phone No_],mt.[Email],mt.[House Apartment No_],mt.[Mobile Phone No_],mt.[Daytime Phone No_]," +
                        "mt.[Ship To Full Name],mt.[Ship To Address],mt.[Ship To Address 2],mt.[Ship To City],mt.[Ship To County],mt.[Ship To Post Code]," +
                        "mt.[Ship To Phone No_],mt.[Ship To Email],mt.[Ship To House Apartment No_],mt.[Ship To Country Region Code]," +
                        "mt.[Click And Collect Order],mt.[Shipping Agent Code],mt.[Shipping Agent Service Code]," +
                        "0 AS Posted," +
                        "(SELECT COUNT(*) FROM [" + navCompanyName + "Customer Order Payment] cop WHERE cop.[Document Id]=mt.[Document Id]) AS CoPay " +
                        "FROM [" + navCompanyName + "Customer Order Header] mt " +
                        "UNION " +
                        "SELECT mt.[Document Id],mt.[Store No_],mt.[Web Trans_ GUID],mt.[Document DateTime] AS Date,mt.[Source Type]," +
                        "mt.[Member Card No_],mt.[Member Contact No_],mt.[Member Contact Name]," +
                        "mt.[FullName],mt.[Address],mt.[Address2],mt.[City],mt.[County],mt.[PostCode],mt.[CountryRegionCode]," +
                        "mt.[PhoneNo],mt.[Email],mt.[HouseApartmentNo],mt.[MobilePhoneNo],mt.[DaytimePhoneNo]," +
                        "mt.[ShipToFullName],mt.[ShipToAddress],mt.[ShipToAddress2],mt.[ShipToCity],mt.[ShipToCounty],mt.[ShipToPostCode]," +
                        "mt.[ShipToPhoneNo],mt.[ShipToEmail],mt.[ShipToHouseApartmentNo],mt.[ShipToCountryRegionCode]," +
                        "mt.[ClickAndCollectOrder],mt.[Shipping Agent Code],mt.[Shipping Agent Service Code]," +
                        "1 AS Posted," +
                        "(SELECT COUNT(*) FROM[" + navCompanyName + "Posted Customer Order Payment] cop WHERE cop.[Document Id]=mt.[Document Id]) AS CoPay " +
                        "FROM [" + navCompanyName + "Posted Customer Order Header] mt " +
                        ") AS Orders ";
            }
        }

        public SalesEntry OrderGetById(string id, bool includeLines, bool external)
        {
            SalesEntry order = null;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlCommand command = connection.CreateCommand())
                {
                    string where = external ? (NavVersion > new Version("13.5") ? "External ID" : "Web Trans_ GUID") : documentId;
                    command.Parameters.Clear();
                    command.CommandText = sql + " WHERE [" + where + "]=@id";
                    command.Parameters.AddWithValue("@id", id.ToUpper());
                    TraceSqlCommand(command);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            order = ReaderToSalesEntry(reader, includeLines);
                        }
                        reader.Close();
                    }
                }
                connection.Close();
            }
            return order;
        }

        private List<SalesEntryLine> OrderLinesGet(string id)
        {
            string select = "SELECT ml.[Number],ml.[Variant Code],ml.[Unit of Measure Code],ml.[Line No_],ml.[Line Type]," +
                            "ml.[Net Price],ml.[Price],ml.[Quantity],ml.[Discount Amount],ml.[Discount Percent]," +
                            "ml.[Net Amount],ml.[Vat Amount],ml.[Amount],ml.[Item Description],ml.[Variant Description]" +
                            ",ml.[" + documentId + "]";

            List<SalesEntryLine> list = new List<SalesEntryLine>();
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM ( " + select + " FROM [" + navCompanyName + "Customer Order Line] ml " +
                                            "UNION " + select + " FROM [" + navCompanyName + "Posted Customer Order Line] ml " +
                                          ") AS OrderLines WHERE [" + documentId + "]=@id" +
                                          " ORDER BY [Line No_]";

                    command.Parameters.AddWithValue("@id", id);
                    TraceSqlCommand(command);
                    connection.Open();
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(ReaderToOrderLine(reader));
                        }
                    }
                }
                connection.Close();
            }
            return list;
        }

        private void OrderLinesGetTotals(string orderId, out int itemCount, out decimal totalAmount, out decimal totalNetAmount, out decimal totalDiscount)
        {
            itemCount = 0;
            totalAmount = 0;
            totalNetAmount = 0;
            totalDiscount = 0;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string select = "SELECT [" + documentId + "], SUM([Quantity]) AS Cnt, SUM([Discount Amount]) AS Disc, SUM([Net Amount]) AS NAmt, SUM([Amount]) AS Amt";

                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM (" + select +
                                            " FROM [" + navCompanyName + "Customer Order Line] GROUP BY [" + documentId + "] " +
                                            "UNION " + select +
                                            " FROM [" + navCompanyName + "Posted Customer Order Line] GROUP BY [" + documentId + "] " +
                                            ") AS OrderTotals WHERE [" + documentId + "]=@id";
                    command.Parameters.AddWithValue("@id", orderId);
                    TraceSqlCommand(command);
                    connection.Open();
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            itemCount = SQLHelper.GetInt32(reader["Cnt"]);
                            totalAmount = SQLHelper.GetDecimal(reader, "Amt");
                            totalNetAmount = SQLHelper.GetDecimal(reader, "NAmt");
                            totalDiscount = SQLHelper.GetDecimal(reader, "Disc");
                        }
                    }
                }
                connection.Close();
            }
        }

        private List<SalesEntryPayment> OrderPayGet(string id)
        {
            List<SalesEntryPayment> list = new List<SalesEntryPayment>();
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string select = "SELECT ml.[Store No_],ml.[Line No_],ml.[Pre Approved Amount],ml.[Tender Type]," +
                                "ml.[Card Type],ml.[Currency Code],ml.[Currency Factor],ml.[Pre Approved Valid Date]," +
                                "ml.[" + cardCustNo + "],ml.[" + documentId + "]";

                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM (" + select +
                                          " FROM [" + navCompanyName + "Customer Order Payment] ml " +
                                          " UNION " + select + " FROM [" + navCompanyName + "Posted Customer Order Payment] ml " +
                                          ") AS OrderTotal WHERE [" + documentId + "]=@id";

                    command.Parameters.AddWithValue("@id", id);
                    TraceSqlCommand(command);
                    connection.Open();
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(ReaderToOrderPay(reader));
                        }
                    }
                }
                connection.Close();
            }
            return list;
        }

        private List<SalesEntryDiscountLine> OrderDiscGet(string id)
        {
            List<SalesEntryDiscountLine> list = new List<SalesEntryDiscountLine>();
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string select = "SELECT ml.[Line No_],ml.[Entry No_],ml.[Discount Type],ml.[Offer No_]," +
                                "ml.[Periodic Disc_ Type],ml.[Periodic Disc_ Group],ml.[Description],ml.[Discount Percent],ml.[Discount Amount]" +
                                ",ml.[" + documentId + "]";

                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM (" + select + " FROM [" + navCompanyName + "Customer Order Discount Line] ml " +
                                          "UNION " + select + " FROM [" + navCompanyName + "Posted CO Discount Line] ml " +
                                          ") AS OrderDiscounts WHERE [" + documentId + "]=@id";

                    command.Parameters.AddWithValue("@id", id);
                    TraceSqlCommand(command);
                    connection.Open();
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(ReaderToOrderDisc(reader));
                        }
                    }
                }
                connection.Close();
            }
            return list;
        }

        private SalesEntry ReaderToSalesEntry(SqlDataReader reader, bool includeLines)
        {
            SalesEntry entry = new SalesEntry
            {
                Id = SQLHelper.GetString(reader[documentId]),
                StoreId = SQLHelper.GetString(reader["Store No_"]),
                DocumentRegTime = ConvertTo.SafeJsonDate(SQLHelper.GetDateTime(reader["Date"]), config.IsJson),
                IdType = DocumentIdType.Order,
                CardId = SQLHelper.GetString(reader["Member Card No_"]),
                Status = SalesEntryStatus.Created,
                RequestedDeliveryDate = ConvertTo.SafeJsonDate(DateTime.MinValue, config.IsJson)
            };

            entry.CustomerOrderNo = entry.Id;
            entry.Posted = SQLHelper.GetBool(reader["Posted"]);
            entry.ClickAndCollectOrder = SQLHelper.GetBool(reader["Click And Collect Order"]);
            entry.AnonymousOrder = string.IsNullOrEmpty(entry.CardId);

            entry.ShippingAgentCode = SQLHelper.GetString(reader["Shipping Agent Code"]);
            entry.ShippingAgentServiceCode = SQLHelper.GetString(reader["Shipping Agent Service Code"]);

            if (NavVersion > new Version("13.5"))
            {
                entry.ExternalId = SQLHelper.GetString(reader["External ID"]);

                entry.ContactName = SQLHelper.GetString(reader["Name"]);
                entry.ContactDayTimePhoneNo = SQLHelper.GetString(reader["Daytime Phone No_"]);
                entry.ContactEmail = SQLHelper.GetString(reader["Email"]);
                entry.ContactAddress = new Address()
                {
                    Address1 = SQLHelper.GetString(reader["Address"]),
                    Address2 = SQLHelper.GetString(reader["Address 2"]),
                    HouseNo = SQLHelper.GetString(reader["House_Apartment No_"]),
                    City = SQLHelper.GetString(reader["City"]),
                    County = SQLHelper.GetString(reader["County"]),
                    PostCode = SQLHelper.GetString(reader["Post Code"]),
                    Country = SQLHelper.GetString(reader["Country_Region Code"]),
                    PhoneNumber = SQLHelper.GetString(reader["Phone No_"]),
                    CellPhoneNumber = SQLHelper.GetString(reader["Mobile Phone No_"])
                };

                entry.ShipToName = SQLHelper.GetString(reader["ShipName"]);
                entry.ShipToEmail = SQLHelper.GetString(reader["Ship-to Email"]);
                entry.ShipToAddress = new Address()
                {
                    Address1 = SQLHelper.GetString(reader["Ship-to Address"]),
                    Address2 = SQLHelper.GetString(reader["Ship-to Address 2"]),
                    HouseNo = SQLHelper.GetString(reader["Ship-to House_Apartment No_"]),
                    City = SQLHelper.GetString(reader["Ship-to City"]),
                    County = SQLHelper.GetString(reader["Ship-to County"]),
                    PostCode = SQLHelper.GetString(reader["Ship-to Post Code"]),
                    Country = SQLHelper.GetString(reader["Ship-to Country_Region Code"]),
                    PhoneNumber = SQLHelper.GetString(reader["Ship-to Phone No_"])
                };
            }
            else
            {
                entry.ExternalId = SQLHelper.GetString(reader["Web Trans_ GUID"]);

                entry.ContactName = SQLHelper.GetString(reader["Full Name"]);
                entry.ContactDayTimePhoneNo = SQLHelper.GetString(reader["Daytime Phone No_"]);
                entry.ContactEmail = SQLHelper.GetString(reader["Email"]);
                entry.ContactAddress = new Address()
                {
                    Address1 = SQLHelper.GetString(reader["Address"]),
                    Address2 = SQLHelper.GetString(reader["Address 2"]),
                    HouseNo = SQLHelper.GetString(reader["House Apartment No_"]),
                    City = SQLHelper.GetString(reader["City"]),
                    County = SQLHelper.GetString(reader["County"]),
                    PostCode = SQLHelper.GetString(reader["Post Code"]),
                    Country = SQLHelper.GetString(reader["Country Region Code"]),
                    PhoneNumber = SQLHelper.GetString(reader["Phone No_"]),
                    CellPhoneNumber = SQLHelper.GetString(reader["Mobile Phone No_"])
                };

                entry.ShipToName = SQLHelper.GetString(reader["Ship To Full Name"]);
                entry.ShipToEmail = SQLHelper.GetString(reader["Ship To Email"]);
                entry.ShipToAddress = new Address()
                {
                    Address1 = SQLHelper.GetString(reader["Ship To Address"]),
                    Address2 = SQLHelper.GetString(reader["Ship To Address 2"]),
                    HouseNo = SQLHelper.GetString(reader["Ship To House Apartment No_"]),
                    City = SQLHelper.GetString(reader["Ship To City"]),
                    County = SQLHelper.GetString(reader["Ship To County"]),
                    PostCode = SQLHelper.GetString(reader["Ship To Post Code"]),
                    Country = SQLHelper.GetString(reader["Ship To Country Region Code"]),
                    PhoneNumber = SQLHelper.GetString(reader["Ship To Phone No_"])
                };
            }

            int copay = SQLHelper.GetInt32(reader["CoPay"]);
            entry.ShippingStatus = (entry.ClickAndCollectOrder) ? ShippingStatus.ShippigNotRequired : ShippingStatus.NotYetShipped;

            OrderLinesGetTotals(entry.Id, out int cnt, out decimal amt, out decimal namt, out decimal disc);
            entry.LineItemCount = cnt;
            entry.TotalAmount = amt;
            entry.TotalNetAmount = namt;
            entry.TotalDiscount = disc;

            if (entry.Posted)
            {
                entry.Status = SalesEntryStatus.Complete;
                SalesEntryRepository srepo = new SalesEntryRepository(config, NavVersion);
                srepo.SalesEntryPointsGetTotal(entry.Id, entry.CustomerOrderNo, out decimal rewarded, out decimal used);
                entry.PointsRewarded = rewarded;
                entry.PointsUsedInOrder = used;
            }

            if (includeLines)
            {
                entry.Lines = OrderLinesGet(entry.Id);
                entry.Payments = OrderPayGet(entry.Id);
                entry.DiscountLines = OrderDiscGet(entry.Id);

                ImageRepository imgrep = new ImageRepository(config);
                List<SalesEntryLine> list = new List<SalesEntryLine>();
                foreach (SalesEntryLine line in entry.Lines)
                {
                    SalesEntryLine exline = list.Find(l => l.Id.Equals(line.Id) && l.ItemId.Equals(line.ItemId) && l.VariantId.Equals(line.VariantId) && l.UomId.Equals(line.UomId));
                    if (exline == null)
                    {
                        if (string.IsNullOrEmpty(line.VariantId))
                        {
                            List<ImageView> img = imgrep.ImageGetByKey("Item", line.ItemId, string.Empty, string.Empty, 1, false);
                            if (img != null && img.Count > 0)
                                line.ItemImageId = img[0].Id;
                        }
                        else
                        {
                            List<ImageView> img = imgrep.ImageGetByKey("Item Variant", line.ItemId, line.VariantId, string.Empty, 1, false);
                            if (img != null && img.Count > 0)
                                line.ItemImageId = img[0].Id;
                        }

                        list.Add(line);
                        continue;
                    }

                    SalesEntryDiscountLine dline = entry.DiscountLines.Find(l => l.LineNumber >= line.LineNumber && l.LineNumber < line.LineNumber + 10000);
                    if (dline != null)
                    {
                        // update discount line number to match existing record, as we will sum up the orderlines
                        dline.LineNumber = exline.LineNumber + dline.LineNumber / 100;
                    }

                    exline.Amount += line.Amount;
                    exline.NetAmount += line.NetAmount;
                    exline.DiscountAmount += line.DiscountAmount;
                    exline.TaxAmount += line.TaxAmount;
                    exline.Quantity += line.Quantity;
                }
                entry.Lines = list;
            }

            return entry;
        }

        private SalesEntryLine ReaderToOrderLine(SqlDataReader reader)
        {
            return new SalesEntryLine()
            {
                VariantId = SQLHelper.GetString(reader["Variant Code"]),
                UomId = SQLHelper.GetString(reader["Unit of Measure Code"]),
                Quantity = SQLHelper.GetDecimal(reader, "Quantity"),
                LineNumber = SQLHelper.GetInt32(reader["Line No_"]),
                LineType = (LineType)SQLHelper.GetInt32(reader["Line Type"]),
                ItemId = SQLHelper.GetString(reader["Number"]),
                NetPrice = SQLHelper.GetDecimal(reader, "Net Price"),
                Price = SQLHelper.GetDecimal(reader, "Price"),
                DiscountAmount = SQLHelper.GetDecimal(reader, "Discount Amount"),
                DiscountPercent = SQLHelper.GetDecimal(reader, "Discount Percent"),
                NetAmount = SQLHelper.GetDecimal(reader, "Net Amount"),
                TaxAmount = SQLHelper.GetDecimal(reader, "Vat Amount"),
                Amount = SQLHelper.GetDecimal(reader, "Amount"),
                ItemDescription = SQLHelper.GetString(reader["Item Description"]),
                VariantDescription = SQLHelper.GetString(reader["Variant Description"])
            };
        }

        private SalesEntryPayment ReaderToOrderPay(SqlDataReader reader)
        {
            return new SalesEntryPayment()
            {
                Amount = SQLHelper.GetDecimal(reader, "Pre Approved Amount"),
                LineNumber = SQLHelper.GetInt32(reader["Line No_"]),
                TenderType = SQLHelper.GetString(reader["Tender Type"]),
                CurrencyCode = SQLHelper.GetString(reader["Currency Code"]),
                CurrencyFactor = SQLHelper.GetDecimal(reader, "Currency Factor"),
                CardNo = SQLHelper.GetString(reader[cardCustNo])
            };
        }

        private SalesEntryDiscountLine ReaderToOrderDisc(SqlDataReader reader)
        {
            return new SalesEntryDiscountLine()
            {
                LineNumber = SQLHelper.GetInt32(reader["Line No_"]),
                Description = SQLHelper.GetString(reader["Description"]),
                No = SQLHelper.GetString(reader["Entry No_"]),
                OfferNumber = SQLHelper.GetString(reader["Offer No_"]),
                DiscountAmount = SQLHelper.GetDecimal(reader, "Discount Amount"),
                DiscountPercent = SQLHelper.GetDecimal(reader, "Discount Percent"),
                DiscountType = (DiscountType)SQLHelper.GetInt32(reader["Discount Type"]),
                PeriodicDiscGroup = SQLHelper.GetString(reader["Periodic Disc_ Group"]),
                PeriodicDiscType = (PeriodicDiscType)SQLHelper.GetInt32(reader["Periodic Disc_ Type"])
            };
        }
    }
}
