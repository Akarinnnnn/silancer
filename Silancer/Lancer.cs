﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Silancer
{
    public class LancerSendResult : EventArgs
    {
        public ulong MessageIndex { get; set; }
        public ulong SendCounter { get; set; }
        public ulong SuccessCounter { get; set; }
        public ulong ContinueFailedCounter { get; set; }
        public Ammo MyAmmo { get; set; }
        public bool IsSendSuccessful { get; set; }
        public bool IsNetSuccessful { get; set; }
        public bool IsMegaAttack { get; set; } = false;
    }
    public class Lancer
    {
        // 本地实例身份
        public string ID { get; set; }
        public string Name { get; set; }
        public string Auth
        {
            get
            {
                if (string.IsNullOrEmpty(_auth))
                    _auth = GetAuth(Cookie);
                return _auth;
            }
        }
        public string GetAuth(string cookie)
        {
            string time = ((DateTime.Now.ToUniversalTime().Ticks - 621355968000000000) / 10000000).ToString();
            StringBuilder sub = new StringBuilder();
            foreach (var t in SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes($"{time} {Regex.Match(cookie, @"SAPISID=(.*?);").Groups[1]} https://www.youtube.com")))
            {
                sub.Append(t.ToString("X2"));
            }
            return $"SAPISIDHASH {time}_{sub.ToString().ToLower()}";
        }
        public string _auth = "";

        // WEB参数
        public string Key
        {
            get => key;
            set
            {
                if (value == key) return;
                key = value;
                Host = $"https://www.youtube.com/youtubei/v1/live_chat/send_message?key={@key}";
            }
        }
        private string key = "";
        public string Host { get; set; }
        public string Cookie { get; set; }
        public string Authorization { get; set; }
        public string Onbehalfofuser { get; set; }

        // 私有计数器
        private ulong messageIndex = 0;
        private ulong successCounter = 0;
        private ulong sendCounter = 0;
        private ulong continueFailedCounter = 0;

        /// <summary>
        /// 发送单条消息
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public int SendMessage(string str, Enemy emeny)
        {
            sendCounter++;
            try
            {
                HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(Host);
                httpWebRequest.KeepAlive = true;
                httpWebRequest.Headers.Add("X-Origin", "https://www.youtube.com");
                httpWebRequest.Headers.Add("Origin", "https://www.youtube.com");
                httpWebRequest.Headers.Set(HttpRequestHeader.Authorization, Authorization);
                httpWebRequest.Headers.Set(HttpRequestHeader.Cookie, Cookie);
                httpWebRequest.Headers.Set(HttpRequestHeader.UserAgent, "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/85.0.4183.102 Safari/537.36");
                httpWebRequest.Method = "POST";
                httpWebRequest.ServicePoint.Expect100Continue = false;
                string s = string.Concat(new string[]
                {
                    "{\"context\":{\"client\":{ \"clientName\":\"WEB\",\"clientVersion\":\"2.20201023.02.00\"},\"request\":{ },\"user\":{ \"onBehalfOfUser\":\"",
                    Onbehalfofuser,
                    "\"}},\"params\":\"",
                    emeny.Params,
                    "\",\"richMessage\":{ \"textSegments\":[{\"text\":\"",
                    str,
                    "\"}]}}"
                });
                using (Stream requestStream = httpWebRequest.GetRequestStream())
                using (StreamWriter sr = new StreamWriter(requestStream))
                    sr.Write(s);
                using HttpWebResponse response = (HttpWebResponse)httpWebRequest.GetResponse();
                using Stream resStream = response.GetResponseStream();
                string resContent = "";
                using (StreamReader sr = new StreamReader(resStream))
                    resContent = sr.ReadToEnd();
                if (!resContent.Contains("addChatItemAction"))
                    return 0;
            }
            catch (WebException)
            {
                return -2;
            }
            catch (Exception)
            {
                return -1;
            }
            return 1;
        }

        public int MegaAttack(string str, Enemy enemy)
        {
            List<Task> tasks = new List<Task>();
            bool end = false;
            int counter = 0;
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            while (!end)
            {
                Task t = new Task(() =>
                {
                    if (counter >= 400) { end = true; return; }
                    if (SendMessage(str, enemy) > 0)
                    {
                        end = true;
                    }
                }, cancellationTokenSource.Token);
                t.Start();
                tasks.Add(t);
                Interlocked.Increment(ref counter);
                Thread.Sleep(100);
            }
            cancellationTokenSource.Cancel();
            return counter;
        }
        #region 命令
        public bool Command(AttackMode mode, Ammo ammo)
        {
            if (string.IsNullOrEmpty(ammo.Content)) return true;
            try
            {
                CmdsQueue.Enqueue(new AttackCommand() { MyAmmo = ammo, Mode = mode });
            }
            catch
            {
                return false;
            }
            return true;
        }
        public ConcurrentQueue<AttackCommand> CmdsQueue { get; private set; } = new ConcurrentQueue<AttackCommand>();
        #endregion

        #region 进程
        public bool ReadyToStop { get; set; } = false;
        private void Thread_ReadingIn()
        {
            while (!ReadyToStop)
            {
                while (CmdsQueue.Count < 1) Thread.Sleep(40);
                if (!CmdsQueue.TryDequeue(out AttackCommand cmd)) continue;
                if (string.IsNullOrEmpty(cmd.MyAmmo.Content)) continue;
                messageIndex += 1;
                bool isNetSuccessful = false;
                bool isSendSuccessful = false;
                switch (cmd.Mode)
                {
                    case AttackMode.Normal:
                        {
                            int f = SendMessage(cmd.MyAmmo.Content, cmd.Enemy);
                            if (f < 0)
                                continueFailedCounter += 1;
                            else if (f == 0)
                                isNetSuccessful = true;
                            else
                            {
                                continueFailedCounter = 0;
                                successCounter += 1;
                                isNetSuccessful = true;
                                isSendSuccessful = true;
                            }
                            break;
                        }
                    case AttackMode.MegaAttack:
                        {
                            int f = MegaAttack(cmd.MyAmmo.Content, cmd.Enemy);
                            if (f == 0)
                                continueFailedCounter += 1;
                            else
                            {
                                continueFailedCounter = 0;
                                successCounter += 1;
                                isNetSuccessful = true;
                                isSendSuccessful = true;
                            }
                            break;
                        }
                }
                LancerSendResult args = new LancerSendResult()
                {
                    MyAmmo = cmd.MyAmmo,
                    MessageIndex = messageIndex,
                    IsMegaAttack = cmd.Mode == AttackMode.MegaAttack,
                    SendCounter = sendCounter,
                    SuccessCounter = successCounter,
                    IsNetSuccessful = isNetSuccessful,
                    IsSendSuccessful = isSendSuccessful,
                    ContinueFailedCounter = continueFailedCounter
                };
                if (isNetSuccessful && isSendSuccessful) SendSucceeded?.Invoke(this, args);
                else SendFailed.Invoke(this, args);
            }
        }
        public Thread MyThread { get; private set; }
        #endregion

        #region 委托与事件
        public delegate void LancerSendEventHandler(Lancer sender, LancerSendResult args);
        public event LancerSendEventHandler SendSucceeded;
        public event LancerSendEventHandler SendFailed;
        #endregion

        #region 结构与析构
        public Lancer(Dictionary<string, string> setting)
        {
            Dictionary<string, string> tempDic = setting;
            Name = tempDic["Name"];
            Key = tempDic["Key"];
            Cookie = tempDic["Cookie"];
            Authorization = GetAuth(Cookie);
            try
            {
                Onbehalfofuser = tempDic["Onbehalfofuser"];
            }
            catch
            {

            }
            MyThread = new Thread(new ThreadStart(Thread_ReadingIn));
            MyThread.Start();
        }
        ~Lancer()
        {
            ReadyToStop = true;
            MyThread.Join();
            MyThread = null;
        }
        #endregion
    }
    public class Enemy
    {
        public string ChannelID { get; set; }
        public string LiveID { get; set; }
        public string Params
        {
            get
            {
                if (string.IsNullOrEmpty(_param))
                    _param = GetParams(ChannelID, LiveID);
                return _param;
            }
        }

        private string _param = null;
        public string GetParams(string channelID, string liveID)
        {
            var a = Encoding.ASCII.GetBytes("\x0a\x38\x0a\x0d\x0a\x0b");
            var b = Encoding.ASCII.GetBytes("*'\x0a\x18");
            var c = Encoding.ASCII.GetBytes("\x12\x0b");
            var d = Encoding.ASCII.GetBytes("\x10\x01\x18\x04");
            var channelCode = Encoding.UTF8.GetBytes(channelID);
            var liveCode = Encoding.UTF8.GetBytes(liveID);
            byte[] src = new byte[a.Length + b.Length + c.Length + d.Length + 2 * liveCode.Length + channelCode.Length];
            int pnt = 0;
            List<byte[]> list = new List<byte[]> { a, liveCode, b, channelCode, c, liveCode, d };
            foreach (var ba in list)
            {
                ba.CopyTo(src, pnt);
                pnt += ba.Length;
            }
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(Convert.ToBase64String(src).Trim('=') + "%3D")).Trim('=');
        }
        public Enemy(string channelID, string liveID)
        {
            ChannelID = channelID;
            LiveID = liveID;
        }
    }
    public class AttackCommand
    {
        public AttackMode Mode { get; set; }
        public Ammo MyAmmo { get; set; }
        public Enemy Enemy { get; set; }
    }
    public enum AttackMode
    {
        Normal, MegaAttack
    }
}