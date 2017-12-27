using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace RemoteService
{

    [ServiceContract()]
    public interface IRemoteService
    {
        [OperationContract]
        DeviceInfo[] GetDevices();

        [OperationContract]
        bool RunCommand(string DeviceName, string Command, Dictionary<string, string> Parameters);
        [OperationContract]
        string RequestData(string Name, string Parameter);

    }
    [Serializable]
    public struct DeviceInfo
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public CommandInfo[] Commands { get; set; }
    }
    [Serializable]
    public struct CommandInfo
    {
        public string DeviceInfo { get; set; }
        public string Device { get; set; }
        public string Command { get; set; }
        public CommandParameter[] Parameters { get; set; }
    }
    [Serializable]
    public struct CommandParameter
    {
        public string Parameter { get; set; }
        public string Value { get; set; }
        public string[] Values { get; set; }
    }

    public class IRemoteServiceClient : ClientBase<IRemoteService>
    {
        public IRemoteServiceClient(Binding binding, EndpointAddress remoteAddress) : base(binding, remoteAddress) { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="address"></param>
        /// <param name="timeout">В секундах</param>
        /// <returns></returns>
        public static IRemoteService CreateRemoteService(string address, int timeout = 300)
        {
            var uri = new Uri(address);
            var binding = new NetTcpBinding(SecurityMode.None);
            binding.SendTimeout = new TimeSpan(0, 0, timeout);
            binding.Security.Message.ClientCredentialType = MessageCredentialType.UserName;
            binding.Security.Transport.ClientCredentialType = TcpClientCredentialType.None;

            var client = new IRemoteServiceClient(binding, new EndpointAddress(uri));
            client.ClientCredentials.UserName.UserName = "username";
            client.ClientCredentials.UserName.Password = "password";
            var proxy = client.ChannelFactory.CreateChannel();
            return proxy;
        }
    }
}
