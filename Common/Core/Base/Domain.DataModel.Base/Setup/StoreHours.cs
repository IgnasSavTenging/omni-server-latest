﻿using System;
using System.Runtime.Serialization;

namespace LSRetail.Omni.Domain.DataModel.Base.Setup
{
    [DataContract(Namespace = "http://lsretail.com/LSOmniService/Base/2017")]
    public enum StoreHourType
    {
        [EnumMember]
        MainStore = 0,
        [EnumMember]
        DriveThruWindow = 1,
    }

    [DataContract(Namespace = "http://lsretail.com/LSOmniService/Base/2017")]
    public class StoreHours : IDisposable
    {
        public StoreHours(string storeId)
        {
            StoreId = storeId;
            DayOfWeek = 0;
            NameOfDay = string.Empty;
            Description = string.Empty;
            OpenFrom = new DateTime(1900, 1, 1);
            OpenTo = new DateTime(1900, 1, 1);
            StoreHourtype = StoreHourType.MainStore;
        }

        public StoreHours()
            : this(string.Empty)
        {
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
            }
        }

        [DataMember]
        public StoreHourType StoreHourtype { get; set; }
        [DataMember]
        public string StoreId { get; set; }
        [DataMember]
        public int DayOfWeek { get; set; }
        [DataMember]
        public string NameOfDay { get; set; }
        [DataMember]
        public string Description { get; set; }
        [DataMember]
        public DateTime OpenFrom { get; set; }
        [DataMember]
        public DateTime OpenTo { get; set; }
    }
}
 