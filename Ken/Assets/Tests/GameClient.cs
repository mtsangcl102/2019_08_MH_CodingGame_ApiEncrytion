using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine.TestTools;
using BestHTTP.SocketIO;
using BestHTTP.SocketIO.Transports;
using PlatformSupport.Collections.ObjectModel;
using BestHTTP.SocketIO.JsonEncoders;

public class GameClient
{
    public bool IsConnecting = false;
    public bool IsConnected = false;
    public bool hasRoom = false;
    private string _host = " ";
    private string _uid = "";
    public string roomId = "";
    public List<string> Chats = new List<string>();

    public EventHandler<SocketEvent> OnSocketEvent;
    public SocketEvent _socketEvent = new SocketEvent();
    private Socket _socket;
    private SocketOptions _options = new SocketOptions();
    public delegate void EventMessage(string sender, Packet message);
    public event EventMessage OnEventMessage;


    public class SocketEvent : EventArgs
    {
        public SocketEventType EventType;
    }

    public enum SocketEventType
    {
        Connecting,
        Connected,
    }

     public GameClient(string uri, string uid)
    {
        _host = uri;
        _uid = uid;
    }

    public void Disconnect()
    {
        if(IsConnected || IsConnecting)
        {
            IsConnecting = false;
            IsConnected = false;
            if (_socket.IsOpen)
            {
                _socket.Off();
                _socket.Disconnect();
                _socket = null;
            }
        }
    }

    public void Connect()
    {
        IsConnecting = true;
        IsConnected = false;

        _socketEvent.EventType = SocketEventType.Connecting;

        _options.AdditionalQueryParams = new ObservableDictionary<string, string>();
        _options.AdditionalQueryParams.Add("userId", _uid);
        _options.ConnectWith = TransportTypes.WebSocket;

        Uri _uri = new Uri(_host);
        SocketManager _manager = new SocketManager(_uri, _options);
        _manager.Encoder = new LitJsonEncoder();
        _socket = _manager.Socket;
        _socket.On("connect", OnConnect);
        _socket.On("disconnect", OnDisconnect);
        _socket.On("error", OnError);
        _socket.On("event", OnEvent);

    }


    private void OnConnect(Socket socket, Packet packet, object[] args)
    {
        _socketEvent.EventType = SocketEventType.Connected;
        OnSocketEvent(this, _socketEvent);
        IsConnecting = false;
        IsConnected = true;
    }

    private void OnDisconnect(Socket socket, Packet packet, object[] args)
    {
        Disconnect();
    }

    private void OnError(Socket socket, Packet packet, object[] args)
    {
        Disconnect();
    }
    private void OnEvent(Socket socket, Packet packet, object[] args)
    {
        Dictionary<string, object> response = (Dictionary<string, object>)args[0];
        _socketEvent.EventType = SocketEventType.Connected;

        foreach(KeyValuePair<string, object> tmpObj in response)
        {
            Debug.Log("output response " + tmpObj.Key + " value is " + tmpObj.Value);
            Debug.Log("packet.EventName " + packet.EventName.ToString());
        }

        switch (packet.EventName)
        {
            case "respondGetRoom":
                if ((bool)response["isSuccess"])
                {
                    roomId = (string)response["roomId"];
                }
                else
                {
                    roomId = "";
                }
                OnSocketEvent(this, _socketEvent);
                break;
            case "respondLeaveRoom":
                if ((bool)response["isSuccess"])
                {
                    roomId = "";
                }
                OnSocketEvent(this, _socketEvent);
                break;
            case "respondCreateRoom":
                if ((bool)response["isSuccess"])
                {
                    roomId = (string)response["roomId"];
                    Chats.Clear();
                }
                OnSocketEvent(this, _socketEvent);
                break;
            case "respondJoinRoom":
                if ((bool)response["isSuccess"])
                {
                    roomId = (string)response["roomId"];
                    Chats.Clear();
                }
                OnSocketEvent(this, _socketEvent);
                break;
            case "respondChatInRoom":
                Debug.Log("respondChatInRoom");
                OnSocketEvent(this, _socketEvent);
                break;
            case "respondMessageInRoom":
                Debug.Log("respondMessageinRoom");
                Chats.Add((string)response["message"]);
                OnEventMessage("", packet);
                break;

        }
    }

    internal void GetRoom()
    {
        _socketEvent.EventType = SocketEventType.Connecting;
        _socket.Emit("getRoom");
    }
    internal void LeaveRoom()
    {
        _socketEvent.EventType = SocketEventType.Connecting;
        _socket.Emit("leaveRoom");
    }

    internal void JoinRoom(string roomId)
    {
        _socketEvent.EventType = SocketEventType.Connecting;
        _socket.Emit("joinRoom", roomId);
    }

    internal void CreateRoom()
    {
        _socketEvent.EventType = SocketEventType.Connecting;
        _socket.Emit("createRoom");
    }

    internal void ChatInRoom(string message)
    {
        _socketEvent.EventType = SocketEventType.Connecting;
        _socket.Emit("chatInRoom", message);
    }
}
