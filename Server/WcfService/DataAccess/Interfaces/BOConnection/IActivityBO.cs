﻿using LSRetail.Omni.Domain.DataModel.Activity.Activities;
using LSRetail.Omni.Domain.DataModel.Activity.Client;
using System;
using System.Collections.Generic;

namespace LSOmni.DataAccess.Interface.BOConnection
{
    public interface IActivityBO
    {
        #region Functions

        ActivityResponse ActivityConfirm(ActivityRequest request);
        ActivityResponse ActivityCancel(string activityNo);
        List<AvailabilityResponse> ActivityAvailabilityGet(string locationNo, string productNo, DateTime activityDate, string contactNo, string optionalResource, string promoCode);
        AdditionalCharge ActivityAdditionalChargesGet(string activityNo);
        bool ActivityAdditionalChargesSet(AdditionalCharge request);
        AttributeResponse ActivityAttributesGet(AttributeType type, string linkNo);
        int ActivityAttributeSet(AttributeType type, string linkNo, string attributeCode, string attributeValue);
        string ActivityReservationInsert(Reservation request);
        string ActivityReservationUpdate(Reservation request);
        MembershipResponse ActivityMembershipSell(string contactNo, string membersShipType);
        bool ActivityMembershipCancel(string contactNo, string memberShipNo, string comment);

        #endregion

        #region Data Get (Replication)

        List<ActivityProduct> ActivityProductsGet();
        List<ActivityType> ActivityTypesGet();
        List<ActivityLocation> ActivityLocationsGet();
        List<Booking> ActivityReservationsGet(string reservationNo, string contactNo, string activityType);
        List<ResHeader> ActivityReservationsHeaderGet(string reservationNo, string reservationType, string status, string locationNo, DateTime fromDate);
        List<Promotion> ActivityPromotionsGet();
        List<Allowance> ActivityAllowancesGet(string contactNo);
        List<CustomerEntry> ActivityCustomerEntriesGet(string contactNo, string customerNo);
        List<MemberProduct> ActivityMembershipProductsGet();
        List<SubscriptionEntry> ActivitySubscriptionChargesGet(string contactNo);
        List<AdmissionEntry> ActivityAdmissionEntriesGet(string contactNo);
        List<Membership> ActivityMembershipsGet(string contactNo);

        #endregion
    }
}
