﻿using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PubNubAPI
{
    internal class SusbcribeEventEventArgs : EventArgs
    {
        public PNStatus pnStatus;
        public PNPresenceEventResult pnper;
        public PNMessageResult pnmr;
    }

    public class SubscriptionWorker<U>
    {
        //Allow one instance only
        public SubscriptionWorker (PubNubUnity pn)
        {
            PubNubInstance = pn;
            webRequest = PubNubInstance.GameObjectRef.AddComponent<PNUnityWebRequest> ();
            webRequest.SubWebRequestComplete += WebRequestCompleteHandler;
        }

        private long lastSubscribeTimetoken = 0;
        private long lastSubscribeTimetokenForNewMultiplex = 0;
        private PNUnityWebRequest webRequest;
        private PubNubUnity PubNubInstance { get; set;}


        //private static volatile SubscriptionWorker<U> instance;
        //private static object syncRoot = new System.Object();

        /*public static SubscriptionWorker<U> Instance
        {
            get 
            {
                if (instance == null) 
                {
                    lock (syncRoot) 
                    {
                        if (instance == null) {
                            instance = new SubscriptionWorker<U> ();
                            //instance.webRequest = PubNub.GameObjectRef.AddComponent<PNUnityWebRequest> ();
                            //instance.webRequest.SubWebRequestComplete += instance.WebRequestCompleteHandler;

                        }
                    }
                }

                return instance;
            }
        }*/

        public void Add (PNOperationType pnOpType, object pnBuilder, RequestState<U> reqState, PubNubUnity pn){
            //Abort existing request
            try{
                PubNubInstance = pn;
                Debug.Log("in add:" + reqState.Reconnect + this.PubNubInstance.Test);
                SubscribeRequestBuilder subscribeBuilder = (SubscribeRequestBuilder)pnBuilder;

                //subscribeBuilder.Reconnect = false;
                List<ChannelEntity> subscribedChannels = pn.SubscriptionInstance.AllSubscribedChannelsAndChannelGroups;
                List<ChannelEntity> newChannelEntities;
                List<string> rawChannels = subscribeBuilder.Channels;
                List<string> rawChannelGroups = subscribeBuilder.ChannelGroups;
                PNOperationType opType = PNOperationType.PNSubscribeOperation;
                long timetokenToUse = subscribeBuilder.Timetoken;
                Debug.Log("after add");
                bool channelsOrChannelGroupsAdded = RemoveDuplicatesCheckAlreadySubscribedAndGetChannels (opType, rawChannels, rawChannelGroups, false, out newChannelEntities);

                bool internetStatus = true;
                if ((channelsOrChannelGroupsAdded) && (internetStatus)) {
                    pn.SubscriptionInstance.Add (newChannelEntities);

                    #if (ENABLE_PUBNUB_LOGGING)
                    Helpers.LogChannelEntitiesDictionary ();
                    #endif

                    if (!timetokenToUse.Equals (0)) {
                        lastSubscribeTimetokenForNewMultiplex = timetokenToUse;
                    } else if (subscribedChannels.Count > 0) {
                        lastSubscribeTimetokenForNewMultiplex = lastSubscribeTimetoken;
                    }
                    //AbortPreviousRequest<T> (subscribedChannels);
                    MultiChannelSubscribeRequest (opType, 0, false);
                }
                #if (ENABLE_PUBNUB_LOGGING)
                else {
                LoggingMethod.WriteToLog (string.Format ("MultiChannelSubscribeInit: channelsOrChannelGroupsAdded {1}, internet status {2}",
                        channelsOrChannelGroupsAdded.ToString (), internetStatus.ToString ()), LoggingMethod.LevelInfo, PubNubInstance.PNConfig.LogVerbosity, PubNubInstance.PNConfig.LogVerbosity);
                }
                #endif

                Debug.Log ("channelsOrChannelGroupsAdded" + channelsOrChannelGroupsAdded);
                Debug.Log ("newChannelEntities" + newChannelEntities.Count);
               
                /*EventHandler handler = PubNub.SusbcribeCallback;
                if (handler != null) {
                    Debug.Log ("Raising SusbcribeEvent");
                    handler (typeof(SubscriptionWorker), mea);
                } else {
                    Debug.Log ("SusbcribeEvent null");
                }*/
            }catch (Exception ex){
                Debug.Log (ex.ToString());
            }

        }

        internal bool RemoveDuplicatesCheckAlreadySubscribedAndGetChannels(PNOperationType type, List<string> rawChannels, List<string> rawChannelGroups, bool unsubscribeCheck, out List<ChannelEntity> channelEntities)
        {
            bool bReturn = false;
            bool channelAdded = false;
            bool channelGroupAdded = false;
            channelEntities = new List<ChannelEntity> ();
            if (rawChannels != null && rawChannels.Count > 0) {
                channelAdded = RemoveDuplicatesCheckAlreadySubscribedAndGetChannelsCommon(type, rawChannels, false, unsubscribeCheck, ref channelEntities);
            }

            if (rawChannelGroups != null && rawChannelGroups.Count > 0) {
                channelGroupAdded = RemoveDuplicatesCheckAlreadySubscribedAndGetChannelsCommon(type, rawChannelGroups, true, unsubscribeCheck, ref channelEntities);
            }

            bReturn = channelAdded | channelGroupAdded;

            return bReturn;
        }

        public void RunHeartbeatRequest(Action callback){
        }

        public void RunPresenceHeartbeatRequest(Action callback){
        }


        internal bool RemoveDuplicatesCheckAlreadySubscribedAndGetChannelsCommon(PNOperationType type, List<string> channelsOrChannelGroups, bool isChannelGroup, bool unsubscribeCheck, ref List<ChannelEntity> channelEntities)
        {
            bool bReturn = false;
            if (channelsOrChannelGroups.Count > 0) {

                channelsOrChannelGroups = channelsOrChannelGroups.Where(x => !string.IsNullOrEmpty(x)).ToList();

                if (channelsOrChannelGroups.Count != channelsOrChannelGroups.Distinct ().Count ()) {
                    channelsOrChannelGroups = channelsOrChannelGroups.Distinct ().ToList ();
                    #if (ENABLE_PUBNUB_LOGGING)
                    LoggingMethod.WriteToLog (string.Format ("RemoveDuplicatesCheckAlreadySubscribedAndGetChannelsCommon: distinct channelsOrChannelGroups len={1}, channelsOrChannelGroups = {2}", 
                         channelsOrChannelGroups.Count, string.Join(",", channelsOrChannelGroups.ToArray())), 
                    LoggingMethod.LevelInfo);
                    #endif

                    string channel = string.Join (",", GetDuplicates (channelsOrChannelGroups).Distinct<string> ().ToArray<string> ());
                    #if (ENABLE_PUBNUB_LOGGING)
                    LoggingMethod.WriteToLog (string.Format ("RemoveDuplicatesCheckAlreadySubscribedAndGetChannelsCommon: duplicates channelsOrChannelGroups {1}", 
                     channel), 
                    LoggingMethod.LevelInfo);
                    #endif

                    string message = string.Format ("Detected and removed duplicate channels {0}", channel); 

                    //PubnubCallbacks.CallErrorCallback<T> (message, errorCallback, PubnubErrorCode.DuplicateChannel, 
                      //  PubnubErrorSeverity.Info, errorLevel);
                }
                #if (ENABLE_PUBNUB_LOGGING)
                LoggingMethod.WriteToLog (string.Format ("RemoveDuplicatesCheckAlreadySubscribedAndGetChannelsCommon: channelsOrChannelGroups len={1}, channelsOrChannelGroups = {2}", 
                     channelsOrChannelGroups.Count, string.Join(",", channelsOrChannelGroups.ToArray())), 
                LoggingMethod.LevelInfo);
                #endif

                bReturn = Helpers.CreateChannelEntityAndAddToSubscribe (type, channelsOrChannelGroups, isChannelGroup, unsubscribeCheck, ref channelEntities, PubNubInstance);
            } else {
                #if (ENABLE_PUBNUB_LOGGING)
                LoggingMethod.WriteToLog (string.Format ("RemoveDuplicatesCheckAlreadySubscribedAndGetChannelsCommon: channelsOrChannelGroups len <=0", 
                DateTime.Now.ToString ()), 
                LoggingMethod.LevelInfo);
                #endif
            }
            return bReturn;
        }

        internal static IEnumerable<string> GetDuplicates(List<string> rawChannels)
        {
            var results = from string a in rawChannels
                group a by a into g
                    where g.Count() > 1
                select g;
            foreach (var group in results)
                foreach (var item in group)
                    yield return item;
        }

        /*public void MultiChannelSubscribeInit<T> (PNOperationType respType, string channel, string channelGroup, long timetokenToUse
            )
        {
            string[] rawChannels = channel.Split (',');
            string[] rawChannelGroups = channelGroup.Split (',');

            List<ChannelEntity> subscribedChannels = Subscription.Instance.AllSubscribedChannelsAndChannelGroups;

            ResetInternetCheckSettings ();

            List<ChannelEntity> newChannelEntities;
            bool channelsOrChannelGroupsAdded = Helpers.RemoveDuplicatesCheckAlreadySubscribedAndGetChannels<T> (respType, null, rawChannels, rawChannelGroups,
                PubnubErrorLevel, false, out newChannelEntities);

            if ((channelsOrChannelGroupsAdded) && (internetStatus)) {
                Subscription.Instance.Add (newChannelEntities);

                #if (ENABLE_PUBNUB_LOGGING)
                Helpers.LogChannelEntitiesDictionary ();
                #endif

                if (!timetokenToUse.Equals (0)) {
                    lastSubscribeTimetokenForNewMultiplex = timetokenToUse;
                } else if (subscribedChannels.Count > 0) {
                    lastSubscribeTimetokenForNewMultiplex = lastSubscribeTimetoken;
                }
                AbortPreviousRequest<T> (subscribedChannels);
                MultiChannelSubscribeRequest<T> (respType, 0, false);
            }
            #if (ENABLE_PUBNUB_LOGGING)
            else {
            LoggingMethod.WriteToLog (string.Format ("MultiChannelSubscribeInit: channelsOrChannelGroupsAdded {1}, internet status {2}",
             channelsOrChannelGroupsAdded.ToString (), internetStatus.ToString ()), LoggingMethod.LevelInfo, PubNubInstance.PNConfig.LogVerbosity);
            }
            #endif
        }*/

        private bool CheckAllChannelsAreUnsubscribed()
        {
            if (PubNubInstance.SubscriptionInstance.AllSubscribedChannelsAndChannelGroups.Count <=0)
            {
                /*StopHeartbeat<T>();
                if (isPresenceHearbeatRunning)
                {
                    StopPresenceHeartbeat<T>();
                }
                ExceptionHandlers.MultiplexException -= HandleMultiplexException<T>;*/
                #if (ENABLE_PUBNUB_LOGGING)
                LoggingMethod.WriteToLog(string.Format("CheckAllChannelsAreUnsubscribed: All channels are Unsubscribed. Further subscription was stopped", DateTime.Now.ToString()), LoggingMethod.LevelInfo, PubNubInstance.PNConfig.LogVerbosity);
                #endif
                return true;
            }
            return false;
        }


        long SaveLastTimetoken(long timetoken)
        {
            long lastTimetoken = 0;
            long sentTimetoken = timetoken;
            #if (ENABLE_PUBNUB_LOGGING)
            StringBuilder sbLogger = new StringBuilder();
            sbLogger.AppendFormat("SaveLastTimetoken: lastSubscribeTimetokenForNewMultiplex={0}\n", lastSubscribeTimetokenForNewMultiplex);
            sbLogger.AppendFormat("SaveLastTimetoken: sentTimetoken={0}\n", sentTimetoken.ToString());
            sbLogger.AppendFormat("SaveLastTimetoken: lastSubscribeTimetoken={0}\n", lastSubscribeTimetoken);
            #endif
            /*if (resetTimetoken || uuidChanged)
            {
                lastTimetoken = 0;
                uuidChanged = false;
                resetTimetoken = false;
                #if (ENABLE_PUBNUB_LOGGING)
                sbLogger.AppendFormat("SaveLastTimetoken: resetTimetoken\n");
                #endif
            }
            else
            {*/
                //override lastTimetoken when lastSubscribeTimetokenForNewMultiplex is set.
                //this is done to use the timetoken prior to the latest response from the server
                //and is true in case new channels are added to the subscribe list.
                if (!sentTimetoken.Equals(0) && !lastSubscribeTimetokenForNewMultiplex.Equals(0) && !lastSubscribeTimetoken.Equals(lastSubscribeTimetokenForNewMultiplex))
                {
                    lastTimetoken = lastSubscribeTimetokenForNewMultiplex;
                    lastSubscribeTimetokenForNewMultiplex = 0;
                    #if (ENABLE_PUBNUB_LOGGING)
                    sbLogger.AppendFormat("SaveLastTimetoken: Using lastSubscribeTimetokenForNewMultiplex={0}\n", lastTimetoken);
                    #endif
                }
                else
                    if (sentTimetoken.Equals(0))
                    {
                        lastTimetoken = sentTimetoken;
                        #if (ENABLE_PUBNUB_LOGGING)
                        sbLogger.AppendFormat("SaveLastTimetoken: Using sentTimetoken={0}\n", sentTimetoken);
                        #endif
                    }
                    else
                    {
                        lastTimetoken = sentTimetoken;
                        #if (ENABLE_PUBNUB_LOGGING)
                        sbLogger.AppendFormat("SaveLastTimetoken: Using sentTimetoken={0}\n", sentTimetoken);
                        #endif
                    }
                if (lastSubscribeTimetoken.Equals(lastSubscribeTimetokenForNewMultiplex))
                {
                    lastSubscribeTimetokenForNewMultiplex = 0;
                }
            //}
            #if (ENABLE_PUBNUB_LOGGING)
            LoggingMethod.WriteToLog (string.Format ("{1} ", 
            sbLogger.ToString()), LoggingMethod.LevelInfo, PubNubInstance.PNConfig.LogVerbosity);
            #endif

            return lastTimetoken;
        }

        string filterExpr;
        public string FilterExpr{
            get { return filterExpr; }
            set{
                filterExpr = value;
                //TerminateCurrentSubscriberRequest ();
            }
        }

        private void MultiChannelSubscribeRequest (PNOperationType type, long timetoken, bool reconnect)
        {
            //Exit if the channel is unsubscribed
            Debug.Log("in  MultiChannelSubscribeRequest");
            if (CheckAllChannelsAreUnsubscribed())
            {
                //return;
            }
			List<ChannelEntity> channelEntities = PubNubInstance.SubscriptionInstance.AllSubscribedChannelsAndChannelGroups;

            // Begin recursive subscribe
            try {
                long lastTimetoken = SaveLastTimetoken(timetoken);

                #if (ENABLE_PUBNUB_LOGGING)
                LoggingMethod.WriteToLog (string.Format ("MultiChannelSubscribeRequest: Building request for {1} with timetoken={2}",
                    Helpers.GetNamesFromChannelEntities(channelEntities), lastTimetoken), LoggingMethod.LevelInfo, PubNubInstance.PNConfig.LogVerbosity);
                #endif
                // Build URL
				string channelsJsonState = PubNubInstance.SubscriptionInstance.CompiledUserState;
                //TODO fix and remove
                channelsJsonState = "";

                string channels = GetNamesFromChannelEntities(channelEntities, false);
                string channelGroups = GetNamesFromChannelEntities(channelEntities, true);

                //v2
                string filterExpr = (!string.IsNullOrEmpty(this.FilterExpr)) ? this.FilterExpr : string.Empty;
                Uri requestUrl = BuildRequests.BuildMultiChannelSubscribeRequest (
                    channels,
                    channelGroups, 
                    lastTimetoken.ToString(), 
                    channelsJsonState,
                    "a", 
                    "",
                    filterExpr, 
                    true, 
                    PubNubInstance.PNConfig.Origin, 
                    "", 
                    PubNubInstance.PNConfig.SubscribeKey, 
                    0,
                    PubNubInstance.Version
                );
                
                /*Uri requestUrl = BuildRequests.BuildMultiChannelSubscribeRequest (channels,
                    channelGroups, lastTimetoken.ToString(), channelsJsonState, this.SessionUUID, this.Region,
                    filterExpr, this.ssl, this.Origin, authenticationKey, this.subscribeKey, this.PresenceHeartbeat);*/


                //RequestState<T> pubnubRequestState = BuildRequests.BuildRequestState<T> (channelEntities, type, reconnect,
                    //0, false, Convert.ToInt64 (timetoken.ToString ()), typeof(T));
                // Wait for message
                //ExceptionHandlers.MultiplexException += HandleMultiplexException<T>;

                //UrlProcessRequest<T> (requestUrl, pubnubRequestState);
                Debug.Log ("RunSubscribeRequest coroutine" + requestUrl.OriginalString);

                RequestState<SubscribeBuilder> requestState = new RequestState<SubscribeBuilder> ();
                //requestState.ChannelEntities = channelEntities;
                requestState.RespType = PNOperationType.PNSubscribeOperation;
                requestState.ChannelEntities = channelEntities;

                //PNCallback<T> timeCallback = new PNTimeCallback<T> (callback);
                //http://ps.pndsn.com/v2/presence/sub-key/sub-c-5c4fdcc6-c040-11e5-a316-0619f8945a4f/uuid/UUID_WhereNow?pnsdk=PubNub-Go%2F3.14.0&uuid=UUID_WhereNow
                webRequest.Run<SubscribeBuilder>(requestUrl.OriginalString, requestState, 310, 0);

            } catch (Exception ex) {
                Debug.Log("in  MultiChannelSubscribeRequest" + ex.ToString());
                #if (ENABLE_PUBNUB_LOGGING)
                LoggingMethod.WriteToLog (string.Format ("MultiChannelSubscribeRequest: method:_subscribe \n channel={1} \n timetoken={2} \n Exception Details={3}",
                 Helpers.GetNamesFromChannelEntities(channelEntities), timetoken.ToString (), ex.ToString ()), LoggingMethod.LevelError);
                #endif
                //PubnubCallbacks.CallErrorCallback<T> (ex, channelEntities,
                  //  PubnubErrorCode.None, PubnubErrorSeverity.Critical, PubnubErrorLevel);

                //this.MultiChannelSubscribeRequest (type, timetoken, false);
            }
        }

        SubscribeEnvelope ParseReceiedJSONV2 (RequestState<U> requestState, string jsonString)
        {
            if (!string.IsNullOrEmpty (jsonString)) {
                #if (ENABLE_PUBNUB_LOGGING)
                LoggingMethod.WriteToLog (string.Format ("ParseReceiedJSONV2: jsonString = {1}",  jsonString), LoggingMethod.LevelInfo, PubNubInstance.PNConfig.LogVerbosity);
                #endif
                
                //this doesnt work on JSONFx for Unity in case a string is passed in an variable of type object
                //SubscribeEnvelope resultSubscribeEnvelope = jsonPluggableLibrary.Deserialize<SubscribeEnvelope>(jsonString);
                object resultSubscribeEnvelope = PubNubInstance.JsonLibrary.DeserializeToObject(jsonString);
                SubscribeEnvelope subscribeEnvelope = new SubscribeEnvelope ();

                if (resultSubscribeEnvelope is Dictionary<string, object>) {

                    Dictionary<string, object> message = (Dictionary<string, object>)resultSubscribeEnvelope;
					subscribeEnvelope.TimetokenMeta = Helpers.CreateTimetokenMetadata (message ["t"], "Subscribe TT: ", PubNubInstance.PNConfig.LogVerbosity);
					subscribeEnvelope.Messages = Helpers.CreateListOfSubscribeMessage (message ["m"], PubNubInstance.PNConfig.LogVerbosity);

                    return subscribeEnvelope;
                } else {
                    #if (ENABLE_PUBNUB_LOGGING)

                    LoggingMethod.WriteToLog (string.Format ("ParseReceiedJSONV2: resultSubscribeEnvelope is not dict",
                        DateTime.Now.ToString ()), LoggingMethod.LevelError);

                    #endif

                    return null;
                }
            } else {
                return null;
            }

        }

        void ParseReceiedTimetoken (RequestState<U> requestState, long receivedTimetoken)
        {
            #if (ENABLE_PUBNUB_LOGGING)
            LoggingMethod.WriteToLog (string.Format ("ParseReceiedTimetoken: receivedTimetoken = {1}",
             receivedTimetoken.ToString()),
            LoggingMethod.LevelInfo);
            #endif
            lastSubscribeTimetoken = receivedTimetoken;

            //TODO 
            /*if (!enableResumeOnReconnect) {
                lastSubscribeTimetoken = receivedTimetoken;
            }
            else {
                //do nothing. keep last subscribe token
            }
            if (requestState.Reconnect) {
                if (enableResumeOnReconnect) {
                    //do nothing. keep last subscribe token
                }
                else {
                    lastSubscribeTimetoken = receivedTimetoken;
                }
            }*/
        }

        private void WebRequestCompleteHandler (object sender, EventArgs ea)
        {
            CustomEventArgs<U> cea = ea as CustomEventArgs<U>;
            Debug.Log("in WebRequestCompleteHandler");

            try {
                
                if (cea != null) {
                    Debug.Log("WebRequestCompleteHandler FireEvent");
                    SubscribeEnvelope resultSubscribeEnvelope = null;
                    string jsonString = cea.Message;
                    if (!jsonString.Equals("[]")) {
                        resultSubscribeEnvelope = ParseReceiedJSONV2 (cea.PubnubRequestState, jsonString);
                    }

                    switch (cea.PubnubRequestState.RespType) {
                    case PNOperationType.PNSubscribeOperation:
                    case PNOperationType.PNPresenceOperation:
                        //Helpers.ProcessResponseCallbacksV2<T> (ref resultSubscribeEnvelope, cea.PubnubRequestState, this.cipherKey, PubNubInstance.JsonPluggableLibrary);
                        if ((resultSubscribeEnvelope != null) && (resultSubscribeEnvelope.TimetokenMeta != null)) {
                            ParseReceiedTimetoken (cea.PubnubRequestState, resultSubscribeEnvelope.TimetokenMeta.Timetoken);

                            MultiChannelSubscribeRequest (cea.PubnubRequestState.RespType, resultSubscribeEnvelope.TimetokenMeta.Timetoken, false);
                        }

                        else {
                            #if (ENABLE_PUBNUB_LOGGING)
                            LoggingMethod.WriteToLog (string.Format ("ResponseCallbackNonErrorHandler ERROR: Couldn't extract timetoken, initiating fresh subscribe request. \nJSON response:\n {1}",
                                 jsonString), LoggingMethod.LevelError);
                            #endif
                            MultiChannelSubscribeRequest (cea.PubnubRequestState.RespType, 0, false);
                        }
                        Debug.Log("WebRequestCompleteHandler ");
                        break;
                    default:
                        break;
                    }
                    Debug.Log("cea"+ cea.Message);
                    PNStatus pns = new PNStatus ();
                    //cea.PubnubRequestState.ChannelEntities
                    //pns.AffectedChannels = rawChannels;
                    //pns.AffectedChannelGroups = rawChannelGroups;

                    PNMessageResult pnmr = new PNMessageResult ("a", "b", "p", 11232234, 13431241234, null, "");

                    PNPresenceEventResult pnper = new PNPresenceEventResult ("a", "b", "join", 11232234, 13431241234, null, null, "", 1, "");

                    SusbcribeEventEventArgs mea = new SusbcribeEventEventArgs();
                    mea.pnmr = pnmr;
                    mea.pnper = pnper;
                    mea.pnStatus = pns;

                    PubNubInstance.RaiseEvent (mea);
                    //TODO identify from T instead of request state
                    /*RequestState<T> requestState = cea.PubnubRequestState;        
                    Debug.Log ("inCoroutineCompleteHandler " + requestState.RespType);
                    switch(requestState.RespType){
                    case PNOperationType.PNSubscribeOperation:*/
                        //PNTimeCallback<T> timeCallback = new PNTimeCallback<T> ();

                    /*PNMessageResult pnMessageResult = new PNMessageResult();
                    pnMessageResult.Channel = cea.Message;
                        PNStatus pnStatus = new PNStatus();
                        pnStatus.Error = false;
                        /*if (pnTimeResult is T) {
                        //return (T)pnTimeResult;
                        //Callback((T)pnTimeResult, pnStatus);
                        } else {*/
                        /*try {
                            //return (T)Convert.ChangeType(pnTimeResult, typeof(T));
                            Debug.Log ("Callback");
                        Callback((SubscribeBuilder)Convert.ChangeType(pnTimeResult, typeof(SubscribeBuilder)), pnStatus);

                            Debug.Log ("After Callback");
                        } catch (InvalidCastException ice) {
                            //return default(T);
                            Debug.Log (ice.ToString());
                            throw ice;
                        }
                        //}

                        //T pnTimeResult2 = (T)pnTimeResult as object;
                        //Callback(pnTimeResult2, pnStatus);
                        //PNTimeResult pnTimeResult2 = (T)pnTimeResult;
                        //timeCallback.OnResponse(pnTimeResult, pnStatus);

                        /*if (cea.PubnubRequestState != null) {
                        ProcessCoroutineCompleteResponse<T> (cea);
                        }*/
                       /* break;
                    
                    default:
                        Debug.Log ("default");
                        break;
                    }

                    #if (ENABLE_PUBNUB_LOGGING)
                    else {
                    LoggingMethod.WriteToLog (string.Format ("CoroutineCompleteHandler: PubnubRequestState null", DateTime.Now.ToString ()), LoggingMethod.LevelError);
                    }
                    #endif*/
                }
                #if (ENABLE_PUBNUB_LOGGING)
                else {
                LoggingMethod.WriteToLog (string.Format ("CoroutineCompleteHandler: cea null", DateTime.Now.ToString ()), LoggingMethod.LevelError);
                }
                #endif
            } catch (Exception ex) {
                Debug.Log (ex.ToString());
                #if (ENABLE_PUBNUB_LOGGING)
                LoggingMethod.WriteToLog (string.Format ("CoroutineCompleteHandler: Exception={1}",  ex.ToString ()), LoggingMethod.LevelError);
                #endif

                //ExceptionHandlers.UrlRequestCommonExceptionHandler<T> (ex.Message, cea.PubnubRequestState,
                //  false, false, PubnubErrorLevel);
            }
        }

        internal static string GetNamesFromChannelEntities (List<ChannelEntity> channelEntities, bool isChannelGroup){

            StringBuilder sb = new StringBuilder ();
            if (channelEntities != null) {
                int count = 0;
                foreach (ChannelEntity c in channelEntities) {
                    if (isChannelGroup && c.ChannelID.IsChannelGroup) {
                        if (count > 0) {
                            sb.Append (",");
                        }

                        sb.Append (c.ChannelID.ChannelOrChannelGroupName);
                        count++;
                    } else if (!isChannelGroup && !c.ChannelID.IsChannelGroup) {
                        if (count > 0) {
                            sb.Append (",");
                        }

                        sb.Append (c.ChannelID.ChannelOrChannelGroupName);
                        count++;
                    }
                }
            }
            return sb.ToString();
        }

        internal static string GetNamesFromChannelEntities (List<ChannelEntity> channelEntities){
            StringBuilder sbCh = new StringBuilder ();
            StringBuilder sbChGrp = new StringBuilder ();
            if (channelEntities != null) {
                int countCh = 0;
                int countChGrp = 0;
                foreach (ChannelEntity c in channelEntities) {
                    if (c.ChannelID.IsChannelGroup) {
                        if (countChGrp > 0) {
                            sbChGrp.Append (",");
                        }

                        sbChGrp.Append (c.ChannelID.ChannelOrChannelGroupName);
                        countChGrp++;
                    } else {
                        if (countCh > 0) {
                            sbCh.Append (",");
                        }

                        sbCh.Append (c.ChannelID.ChannelOrChannelGroupName);
                        countCh++;
                    }

                }
            }
            return string.Format ("channel(s) = {0} and channelGroups(s) = {1}", sbCh.ToString(), sbChGrp.ToString());
        }


        /*private void RetryLoop<T> (RequestState<T> pubnubRequestState)
        {
            internetStatus = false;
            retryCount++;
            if (retryCount <= NetworkCheckMaxRetries) {
                string cbMessage = string.Format ("Internet Disconnected, retrying. Retry count {0} of {1}",
                    retryCount.ToString (), NetworkCheckMaxRetries);
                #if (ENABLE_PUBNUB_LOGGING)
                LoggingMethod.WriteToLog (string.Format("RetryLoop: {1}",  cbMessage), LoggingMethod.LevelError);
                #endif
                PubnubCallbacks.FireErrorCallbacksForAllChannels<T> (cbMessage, pubnubRequestState,
                    PubnubErrorSeverity.Warn, PubnubErrorCode.NoInternetRetryConnect, PubnubErrorLevel);

            } else {
                retriesExceeded = true;
                string cbMessage = string.Format ("Internet Disconnected. Retries exceeded {0}. Unsubscribing connected channels.",
                    NetworkCheckMaxRetries);
                #if (ENABLE_PUBNUB_LOGGING)
                LoggingMethod.WriteToLog (string.Format("RetryLoop: {1}",  cbMessage), LoggingMethod.LevelError);
                #endif

                //stop heartbeat.
                StopHeartbeat<T>();
                //reset internetStatus
                ResetInternetCheckSettings();

                coroutine.BounceRequest<T> (CurrentRequestType.Subscribe, null, false);

                PubnubCallbacks.FireErrorCallbacksForAllChannels<T> (cbMessage, pubnubRequestState,
                    PubnubErrorSeverity.Warn, PubnubErrorCode.NoInternetRetryConnect, PubnubErrorLevel);


                MultiplexExceptionHandler<T> (ResponseType.SubscribeV2, true, false);
            }
        }

        internal static bool CreateChannelEntityAndAddToSubscribe <T>(PNOperationType type, string[] rawChannels, 
            bool isChannelGroup,
            PubnubErrorFilter.Level errorLevel, bool unsubscribeCheck, ref List<ChannelEntity> channelEntities
        )
        {
            bool bReturn = false;    
            for (int index = 0; index < rawChannels.Length; index++)
            {
                string channelName = rawChannels[index].Trim();

                if (channelName.Length > 0) {
                    if((type == PNOperationType.PNPresenceOperation) 
                        || (type == PNOperationType.PNPresenceUnsubscribeOperation)) {
                        channelName = string.Format ("{0}{1}", channelName, Utility.PresenceChannelSuffix);
                    }

                    #if (ENABLE_PUBNUB_LOGGING)
                    Helpers.LogChannelEntitiesDictionary();
                    LoggingMethod.WriteToLog (string.Format ("CreateChannelEntityAndAddToSubscribe: channel={1}",  channelName), LoggingMethod.LevelInfo, PubNubInstance.PNConfig.LogVerbosity);
                    #endif

                    //create channelEntity
                    ChannelEntity ce = Helpers.CreateChannelEntity (channelName, true, isChannelGroup, null, 
                        userCallback, connectCallback, errorCallback, disconnectCallback, wildcardPresenceCallback);

                    bool channelIsSubscribed = false;
                    if (Subscription.Instance.ChannelEntitiesDictionary.ContainsKey (ce.ChannelID)){
                        channelIsSubscribed = Subscription.Instance.ChannelEntitiesDictionary [ce.ChannelID].IsSubscribed;
                    }

                    if (unsubscribeCheck) {
                        if (!channelIsSubscribed) {
                            string message = string.Format ("{0}Channel Not Subscribed", (ce.ChannelID.IsPresenceChannel) ? "Presence " : "");
                            PubnubErrorCode errorType = (ce.ChannelID.IsPresenceChannel) ? PubnubErrorCode.NotPresenceSubscribed : PubnubErrorCode.NotSubscribed;
                            #if (ENABLE_PUBNUB_LOGGING)
                            LoggingMethod.WriteToLog (string.Format ("CreateChannelEntityAndAddToSubscribe: channel={1} response={2}",  channelName, message), LoggingMethod.LevelInfo, PubNubInstance.PNConfig.LogVerbosity);
                            #endif
                            PubnubCallbacks.CallErrorCallback<T> (ce, message,
                                errorType, PubnubErrorSeverity.Info, errorLevel);
                        } else {
                            channelEntities.Add (ce);
                            bReturn = true;
                        }
                    } else {
                        if (channelIsSubscribed) {
                            string message = string.Format ("{0}Already subscribed", (ce.ChannelID.IsPresenceChannel) ? "Presence " : "");
                            PubnubErrorCode errorType = (ce.ChannelID.IsPresenceChannel) ? PubnubErrorCode.AlreadyPresenceSubscribed : PubnubErrorCode.AlreadySubscribed;
                            PubnubCallbacks.CallErrorCallback<T> (ce, message,
                                errorType, PubnubErrorSeverity.Info, errorLevel);
                            #if (ENABLE_PUBNUB_LOGGING)
                            LoggingMethod.WriteToLog (string.Format ("CreateChannelEntityAndAddToSubscribe: channel={1} response={2}",  channelName, message), LoggingMethod.LevelInfo, PubNubInstance.PNConfig.LogVerbosity);
                            #endif
                        } else {
                            channelEntities.Add (ce);
                            bReturn = true;
                        }
                    }
                } else {
                    #if (ENABLE_PUBNUB_LOGGING)
                    string message = "Invalid Channel Name";
                    if (isChannelGroup) {
                    message = "Invalid Channel Group Name";
                    }

                    LoggingMethod.WriteToLog(string.Format("CreateChannelEntityAndAddToSubscribe: channel={1} response={2}", DateTime.Now.ToString(), channelName, message), 
                    LoggingMethod.LevelInfo);
                    #endif
                }
            }
            return bReturn;
        }*/

        //RunHeartbeat
    }
}

