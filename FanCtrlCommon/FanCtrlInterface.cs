using System.Runtime.Serialization;
using System.ServiceModel;

namespace FanCtrlCommon
{
    [ServiceContract]
    public interface IFanCtrlInterface
    {
        [OperationContract]
        FanCtrlData GetData();

        [OperationContract]
        uint GetFan1Rpm();

        [OperationContract]
        bool Level2IsForced();

        [OperationContract]
        void SetLevel2IsForced(bool forced);
    }

    [DataContract]
    public struct FanCtrlData
    {
        [DataMember]
        public uint SystemTemperature { get; private set; }

        [DataMember]
        public uint FanLevel { get; private set; }

        public FanCtrlData(uint temp, uint fan)
        {
            SystemTemperature = temp;
            FanLevel = fan;
        }
    }
}