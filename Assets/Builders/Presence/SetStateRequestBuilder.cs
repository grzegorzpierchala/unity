using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace PubNubAPI
{
    public class SetStateRequestBuilder: PubNubNonSubBuilder<SetStateRequestBuilder, PNSetStateResult>, IPubNubNonSubscribeBuilder<SetStateRequestBuilder, PNSetStateResult>
    {
        List<ChannelEntity> ChannelEntities = null;
        private List<string> ChannelsForState { get; set;}
        private List<string> ChannelGroupsForState { get; set;}

        private string uuid { get; set;}
        private Dictionary<string, object> UserState { get; set;}

        public SetStateRequestBuilder(PubNubUnity pn): base(pn){
            Debug.Log ("PNSetStateResult Construct");
        }

        public void UUID(string uuid){
            this.uuid = uuid;
        }

        public void State(Dictionary<string, object> state){
            this.UserState = state;
        }

        public void Channels(List<string> channels){
            ChannelsForState = channels;
        }

        public void ChannelGroups(List<string> channelGroups){
            ChannelGroupsForState = channelGroups;
        }

        #region IPubNubBuilder implementation
        public void Async(Action<PNSetStateResult, PNStatus> callback)
        {
            this.Callback = callback;
            //validate state here
            try{
                if(UserState!=null){
                    Type t = UserState.GetType();
                    bool isDict = t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>);
                    if(!isDict){
                        throw new MissingMemberException ("State is not of type Dictionary<,>");
                    } else {
                        //string userState = "";

                        if (CheckAndAddExistingUserState (
                            ChannelsForState, 
                            ChannelGroupsForState,
                            UserState, 
                            false,
                            uuid, 
                            this.PubNubInstance.PNConfig.UUID,
                            //out userState, 
                            out ChannelEntities
                        )) {
                            Debug.Log ("PNSetStateResult Async");
                            base.Async(callback, PNOperationType.PNSetStateOperation, CurrentRequestType.NonSubscribe, this);

                            //SharedSetUserState(ChannelsForState, ChannelGroupsForState, channelEntities, uuid, UserState);
                        } else {
                            Debug.Log ("PNSetStateResult Else");
                        }
                    }

                }
                /*if (!this.PubNubInstance.JsonLibrary.IsDictionaryCompatible (state)) {
                    
                } else {
                    Dictionary<string, object> deserializeUserState = this.PubNubInstance.JsonLibrary.DeserializeToDictionaryOfObject (jsonUserState);
                    if (deserializeUserState == null) {
                        throw new MissingMemberException ("Missing JSON formatted user state");
                    } else {
                        string userState = "";
                        List<ChannelEntity> channelEntities;
                        if (Helpers.CheckAndAddExistingUserState<T> (channel, channelGroup,
                            deserializeUserState, userCallback, errorCallback, errorLevel, false,
                            uuid, this.SessionUUID,
                            out userState, out channelEntities
                        )) {
                            SharedSetUserState<T> (channel, channelGroup,
                                channelEntities, uuid, userState);
                        }
                    }
                }*/

            } catch (Exception ex){
                Debug.Log(ex.ToString());
            }
            

        }
        #endregion

        protected override void RunWebRequest(QueueManager qm){
            RequestState<PNSetStateResult> requestState = new RequestState<PNSetStateResult> ();
            requestState.RespType = PNOperationType.PNWhereNowOperation;

            string channels = "";
            if((ChannelsForState != null) && (ChannelsForState.Count>0)){
                channels = String.Join(",", ChannelsForState.ToArray());
            }

            string channelGroups = "";
            if((ChannelGroupsForState != null) && (ChannelGroupsForState.Count>0)){
                channelGroups = String.Join(",", ChannelGroupsForState.ToArray());
            }

            if (string.IsNullOrEmpty (uuid)) {
                uuid = this.PubNubInstance.PNConfig.UUID;
            }

            Uri request = BuildRequests.BuildSetStateRequest(
                channels,
                channelGroups,
                Helpers.BuildJsonUserState(ChannelEntities),
                uuid,
                this.PubNubInstance.PNConfig.UUID,
                this.PubNubInstance.PNConfig.Secure,
                this.PubNubInstance.PNConfig.Origin,
                this.PubNubInstance.PNConfig.AuthKey,
                this.PubNubInstance.PNConfig.SubscribeKey,
                this.PubNubInstance.Version
            );
            this.PubNubInstance.PNLog.WriteToLog(string.Format("Run PNSetStateResult {0}", request.OriginalString), PNLoggingMethod.LevelInfo);
            base.RunWebRequest(qm, request, requestState, this.PubNubInstance.PNConfig.NonSubscribeTimeout, 0, this); 
        }

        internal bool UpdateOrAddUserStateOfEntity(string channel, bool isChannelGroup, Dictionary<string, object> userState, bool edit, bool isForOtherUUID, ref List<ChannelEntity> channelEntities)
        {
            ChannelEntity ce = Helpers.CreateChannelEntity (channel, false, isChannelGroup, userState);
            bool stateChanged = false;

            if (isForOtherUUID) {
                ce.ChannelParams.UserState = userState;
                channelEntities.Add (ce);
                stateChanged = true;
            } else {
                stateChanged = this.PubNubInstance.SubscriptionInstance.UpdateOrAddUserStateOfEntity (ref ce, userState, edit);
                if (!stateChanged) {
                    string message = "No change in User State";

                    //PubnubCallbacks.CallErrorCallback<T> (ce, message,
                      //  PubnubErrorCode.UserStateUnchanged, PubnubErrorSeverity.Info, errorLevel);
                } else {
                    channelEntities.Add (ce);
                }
            }
            return stateChanged;
        }

        internal bool CheckAndAddExistingUserState(List<string> channels, List<string> channelGroups, Dictionary<string, object> userState, bool edit, string uuid, string sessionUUID, out List<ChannelEntity> channelEntities)
        {
            bool stateChanged = false;
            bool isForOtherUUID = false;
            channelEntities = new List<ChannelEntity> ();
            if (!string.IsNullOrEmpty (uuid) && !sessionUUID.Equals (uuid)) {
                isForOtherUUID = true;
            } 
            foreach (string ch in channels) {
                if (!string.IsNullOrEmpty (ch)) {
                    bool changeState = UpdateOrAddUserStateOfEntity (ch, false, userState, edit, isForOtherUUID, ref channelEntities);
                    if (changeState && !stateChanged) {
                        stateChanged = true;
                    }
                }
            }

            foreach (string ch in channelGroups) {
                if (!string.IsNullOrEmpty (ch)) {
                    bool changeState = UpdateOrAddUserStateOfEntity (ch, true, userState, edit, isForOtherUUID, ref channelEntities);
                    if (changeState && !stateChanged) {
                        stateChanged = true;
                    }
                }
            }

            //returnUserState = Helpers.BuildJsonUserState(channelEntities);

            return stateChanged;
        }

        protected override void CreatePubNubResponse(object deSerializedResult){
            //{"status": 200, "message": "OK", "payload": {"channels": {"channel1": {"k": "v"}, "channel2": {}}}, "uuid": "pn-c5a12d424054a3688066572fb955b7a0", "service": "Presence"}

            //TODO read all values.
            
            PNSetStateResult pnGetStateResult = new PNSetStateResult();
            //pnGetStateResult
            
            Dictionary<string, object> dictionary = deSerializedResult as Dictionary<string, object>;
            PNStatus pnStatus = new PNStatus();

            if (dictionary!=null && dictionary.ContainsKey("error") && dictionary["error"].Equals(true)){
                pnGetStateResult = null;
                pnStatus.Error = true;
                //TODO create error data
            } else if(dictionary!=null) {
                string log = "";
                object objPayload;
                dictionary.TryGetValue("payload", out objPayload);

                if(objPayload!=null){
                    Dictionary<string, object> payload = objPayload as Dictionary<string, object>;
                    object objChannelsDict;
                    payload.TryGetValue("channels", out objChannelsDict);
                    //TODO NO CG
                    //payload.TryGetValue("channelGroups", out objChannelsDict);

                    if(objChannelsDict!=null){
                        Dictionary<string, object> channelsDict = objPayload as Dictionary<string, object>;
                        foreach(KeyValuePair<string, object> kvp in channelsDict){
                            Debug.Log("KVP:" + kvp.Key + kvp.Value);
                        }
                    } 
               
                } else {
                    pnStatus.Error = true;
                }
            } else {
                pnGetStateResult = null;
                pnStatus.Error = true;
            }
            Callback(pnGetStateResult, pnStatus);
        }
       
    }
}

