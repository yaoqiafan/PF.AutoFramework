using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Events
{
    public class ClientConnectedEventArgs : EventArgs
    {
        public string ClientId { get; }
        public string ServerAddress { get; }
        public DateTime ConnectTime { get; }

        public ClientConnectedEventArgs(string clientId, string serverAddress)
        {
            ClientId = clientId;
            ServerAddress = serverAddress;
            ConnectTime = DateTime.Now;
        }
    }

    public class ClientDisconnectedEventArgs : EventArgs
    {
        public string ClientId { get; }
        public string Reason { get; }
        public DateTime DisconnectTime { get; }

        public ClientDisconnectedEventArgs(string clientId, string reason)
        {
            ClientId = clientId;
            Reason = reason;
            DisconnectTime = DateTime.Now;
        }
    }

    public class DataReceivedEventArgs : EventArgs
    {
        public string ClientId { get; }
        public byte[] Data { get; }
        public DateTime ReceiveTime { get; }

        public DataReceivedEventArgs(string clientId, byte[] data)
        {
            ClientId = clientId;
            Data = data;
            ReceiveTime = DateTime.Now;
        }
    }

    public class ErrorOccurredEventArgs : EventArgs
    {
        public string ClientId { get; }
        public string ErrorMessage { get; }
        public Exception Exception { get; }
        public DateTime ErrorTime { get; }

        public ErrorOccurredEventArgs(string clientId, string errorMessage, Exception exception)
        {
            ClientId = clientId;
            ErrorMessage = errorMessage;
            Exception = exception;
            ErrorTime = DateTime.Now;
        }
    }
}
