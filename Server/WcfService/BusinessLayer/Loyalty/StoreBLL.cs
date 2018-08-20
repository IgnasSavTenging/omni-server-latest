﻿using System;
using System.Collections.Generic;
using LSOmni.DataAccess.Interface.Repository.Loyalty;
using LSRetail.Omni.Domain.DataModel.Base.Setup;
using LSRetail.Omni.Domain.DataModel.Base;
using LSRetail.Omni.Domain.DataModel.Base.Requests;

namespace LSOmni.BLL.Loyalty
{
    public class StoreBLL : BaseLoyBLL
    {
        public StoreBLL(string securityToken, int timeoutInSeconds)
            : base(securityToken, timeoutInSeconds)
        {
        }

        public StoreBLL(int timeoutInSeconds)
            : this("", timeoutInSeconds)
        {
        }

        public virtual List<Store> StoresGetAll(bool clickAndCollectOnly)
        {
            List<Store> storelist = BOLoyConnection.StoresGetAll(clickAndCollectOnly);
            foreach (Store store in storelist)
            {
                store.StoreServices = base.BOLoyConnection.StoreServicesGetByStoreId(store.Id);
            }
            return storelist;
        }

        public virtual Store StoreGetById(string id)
        {
            Store store = BOLoyConnection.StoreGetById(id);

            IAppSettingsRepository iAppRepo = GetDbRepository<IAppSettingsRepository>();
            int offset = iAppRepo.AppSettingsIntGetByKey(AppSettingsKey.Timezone_HoursOffset_DD);
            int dayOfWeekOffset = 1;
            if (iAppRepo.AppSettingsKeyExists(AppSettingsKey.Timezone_DayOfWeekOffset))
                dayOfWeekOffset = iAppRepo.AppSettingsIntGetByKey(AppSettingsKey.Timezone_DayOfWeekOffset);

            store.StoreHours = BOLoyConnection.StoreHoursGetByStoreId(id, offset, dayOfWeekOffset);
            return store;
        }

        public virtual List<Store> StoresGetByCoordinates(double latitude, double longitude, double maxDistance, int maxNumberOfStores)
        {
            return BOLoyConnection.StoresLoyGetByCoordinates(latitude, longitude, maxDistance, maxNumberOfStores, Store.DistanceType.Kilometers);
        }

        public virtual List<Store> StoresGetbyItemInStock(string itemId, string variantId, double latitude, double longitude, double maxDistance, int maxNumberOfStores)
        {
            List<Store> storeListOut = new List<Store>();
            //CALL NAV web service 
            List<string> locationIds = new List<string>();
            //get locationIds from latitude and longitude(if != 0) ..
            if (latitude == 0 || longitude == 0)
                maxDistance = 9000000000000.0; //so all stores are returned

            List<Store> storeListByCoords = BOLoyConnection.StoresLoyGetByCoordinates(latitude, longitude, maxDistance, maxNumberOfStores, Store.DistanceType.Kilometers);
            foreach (Store store in storeListByCoords)
            {
                locationIds.Add(store.Id);
            }

            List<InventoryResponse> storeList = BOAppConnection.ItemsInStockGet(itemId, variantId, 0, locationIds, true);
            //now add the distance before sending it back to client
            foreach (InventoryResponse store in storeList)
            {
                foreach (Store storeWithDistance in storeListByCoords)
                {
                    if (storeWithDistance.Id == store.StoreId)
                    {
                        storeListOut.Add(storeWithDistance);
                        break;
                    }
                }
            }
            return storeListOut;
        }
    }
}