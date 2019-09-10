using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using BestHTTP;
using BestHTTP.SocketIO;
using BestHTTP.SocketIO.Events;
using Tests;


public class GameClientHelper
{
    public static IEnumerator AssertConnect(GameClient gameClient)
    {
        
        EventHandler<GameClient.SocketEvent> handler = null;
        Assert.AreEqual(gameClient.IsConnected, false);
        Assert.AreEqual(gameClient.IsConnecting, false);

        gameClient.OnSocketEvent += handler = (sender, e) =>
        {
            Assert.AreEqual(e.EventType, GameClient.SocketEventType.Connected);
            gameClient.OnSocketEvent -= handler;
            handler = null;
        };
        gameClient.Connect();

        Assert.AreEqual(gameClient.IsConnecting, true);

        yield return TestHelper.Timeout(() => handler == null);

        Assert.AreEqual(gameClient.IsConnected, true);
        Assert.AreEqual(gameClient.IsConnecting, false);
        
    }

    public static IEnumerator AssertInvalidConnect(GameClient gameClient)
    {
        yield return null ; 
    }

    public static IEnumerator AssertLeaveIfHasRoom(GameClient gameClient)
    {
        EventHandler<GameClient.SocketEvent> handler = null;
        gameClient.OnSocketEvent += handler = (sender, e) =>
        {
            Assert.AreEqual(e.EventType, GameClient.SocketEventType.Connected);
            gameClient.OnSocketEvent -= handler;
            handler = null;
        };
        gameClient.GetRoom();
        yield return TestHelper.Timeout(() => handler == null);

        if (gameClient.roomId == "")
        {
            Assert.IsTrue(true);
        }
        else
        {
            gameClient.LeaveRoom();
            gameClient.OnSocketEvent += handler = (sender, e) =>
            {
                Assert.AreEqual(e.EventType, GameClient.SocketEventType.Connected);
                gameClient.OnSocketEvent -= handler;
                handler = null;
            };
            yield return TestHelper.Timeout(() => handler == null);
            Assert.IsTrue(gameClient.roomId == "");
        }
    }

    public static IEnumerator AssertDisconnect(GameClient gameClient)
    {
        Assert.IsTrue(gameClient.IsConnected);
        gameClient.Disconnect();
        Assert.IsFalse(gameClient.IsConnected);
        Assert.IsFalse(gameClient.hasRoom);
        yield return null;

    }

    public static IEnumerator AssertCreateRoom(GameClient gameClient)
    {
        EventHandler<GameClient.SocketEvent> handler = null;
        gameClient.OnSocketEvent += handler = (sender, e) =>
        {
            Assert.AreEqual(e.EventType, GameClient.SocketEventType.Connected);
            gameClient.OnSocketEvent -= handler;
            handler = null;
        };

        gameClient.CreateRoom();

        yield return TestHelper.Timeout(() => handler == null);

        Assert.IsTrue(gameClient.roomId != "" );

    }

    public static IEnumerator AssertHasRoom(GameClient gameClient)
    {
        EventHandler<GameClient.SocketEvent> handler = null;
        gameClient.OnSocketEvent += handler = (sender, e) =>
        {
            Assert.AreEqual(e.EventType, GameClient.SocketEventType.Connected);
            gameClient.OnSocketEvent -= handler;
            handler = null;
        };
        gameClient.GetRoom();

        yield return TestHelper.Timeout(() => handler == null);

        Assert.IsTrue(gameClient.roomId != "");
    }
    public static IEnumerator AssertLeaveRoom(GameClient gameClient)
    {
        EventHandler<GameClient.SocketEvent> handler = null;
        gameClient.OnSocketEvent += handler = (sender, e) =>
        {
            Assert.AreEqual(e.EventType, GameClient.SocketEventType.Connected);
            gameClient.OnSocketEvent -= handler;
            handler = null;
        };
        gameClient.LeaveRoom();

        yield return TestHelper.Timeout(() => handler == null);
        Assert.IsFalse(gameClient.hasRoom);
    }

    public static IEnumerator AssertJoinRoom(GameClient gameClient, string roomId)
    {
        EventHandler<GameClient.SocketEvent> handler = null;
        gameClient.OnSocketEvent += handler = (sender, e) =>
        {
            Assert.AreEqual(e.EventType, GameClient.SocketEventType.Connected);
            gameClient.OnSocketEvent -= handler;
            handler = null;
        };
        gameClient.JoinRoom(roomId);

        yield return TestHelper.Timeout(() => handler == null);
        Assert.IsTrue(gameClient.roomId == roomId);
    }

    public static IEnumerator AssertInvalidJoinRoom(GameClient gameClient, string roomId)
    {
        EventHandler<GameClient.SocketEvent> handler = null;
        gameClient.OnSocketEvent += handler = (sender, e) =>
        {
            Assert.AreEqual(e.EventType, GameClient.SocketEventType.Connected);
            gameClient.OnSocketEvent -= handler;
            handler = null;
        };
        gameClient.JoinRoom(roomId);

        yield return TestHelper.Timeout(() => handler == null);
        Assert.IsTrue(gameClient.roomId != roomId);
    }

    public static IEnumerator AssertChatInRoom(GameClient gameClient, string message)
    {
        EventHandler<GameClient.SocketEvent> handler = null;
        gameClient.OnSocketEvent += handler = (sender, e) =>
        {
            Assert.AreEqual(e.EventType, GameClient.SocketEventType.Connected);
            gameClient.OnSocketEvent -= handler;
            handler = null;
        };
        gameClient.ChatInRoom(message);
        yield return TestHelper.Timeout(() => handler == null);
    }
}