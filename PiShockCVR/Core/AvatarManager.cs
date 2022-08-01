using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Core.Networking.IO.Social;

using PiShockCVR.Config;

namespace PiShockCVR.Core
{
    public static class AvatarManager
    {
        private static readonly string PiShockApi = "https://do.pishock.com/api/apioperate";
        private static readonly List<PiShockDevice> Devices = new List<PiShockDevice>();
        private static readonly HttpClient WebHandler = new HttpClient();

        private static ClientWebSocket WebSocketClient = new ClientWebSocket();
        private static float elapsedTime = 0f;

        public static void Init()
        {
            InternalEvents.OnLocalAvatarInstantiated += OnAvatarReady;
        }

        private static void SendAPIRequest(PiShockDevice device, PiShockPoint point, string user)
        {
            string request = "{\"Username\":\"" + Configuration.Username.Value
                    + "\",\"Apikey\":\"" + Configuration.ApiKey.Value
                    + "\",\"Name\":\"" + "[ChilloutVR] " + user
                    + "\",\"Code\":\"" + device.Link.ShareCode
                    + "\",\"Intensity\":\"" + (point.Strength ?? Configuration.DefaultStrength.Value)
                    + "\",\"Duration\":\"" + (point.Duration ?? Configuration.DefaultDuration.Value)
                    + "\",\"Op\":\"" + (int)(point.Type ?? Configuration.ParsedDefaultType) + "\"}";

            if (Configuration.LogApiRequests.Value)
                PiShockCVRMod.Logger.Msg("[PiShock API] Sending => " + request);

            Task.Run(async () =>
            {
                await WebHandler.PostAsync(PiShockApi, new StringContent(request, Encoding.UTF8, "application/json"));
            }).ContinueWith(t =>
            {
                if (t.IsFaulted)
                    PiShockCVRMod.Logger.Error("Task raised exception: " + t.Exception);
            });
        }

        private static void SendLocalRequest(PiShockDevice device, PiShockPoint point)
        {
            string request = (int)(point.Type ?? Configuration.ParsedDefaultType) + ","
                + (point.Strength ?? Configuration.DefaultStrength.Value) + ","
                + (point.Duration ?? Configuration.DefaultDuration.Value) + ","
                + Configuration.LocalPiShockId.Value + ","
                + device.Link.DeviceId;

            if (Configuration.LogApiRequests.Value)
                PiShockCVRMod.Logger.Msg("[PiShock Local] Sending => " + request);

            CancellationTokenSource cts = new CancellationTokenSource(5000);
            Task.Run(async () =>
            {
                if (WebSocketClient.State == WebSocketState.Open)
                {
                    if (await SendWebSocketMessageAsync(request, cts))
                        return;

                    cts.Dispose();
                    cts = new CancellationTokenSource(5000);
                }

                WebSocketClient.Dispose();
                WebSocketClient = new ClientWebSocket();

                Task task = WebSocketClient.ConnectAsync(new Uri("ws://" + Configuration.LocalAddress.Value + ":8000"), cts.Token);
                while (!(task.IsCompleted || cts.IsCancellationRequested))
                    await Task.Delay(100);

                cts.Dispose();
                cts = new CancellationTokenSource(5000);

                if (WebSocketClient.State == WebSocketState.Open)
                    await SendWebSocketMessageAsync(request, cts);

            }).ContinueWith(t =>
            {
                if (t.IsFaulted)
                    PiShockCVRMod.Logger.Error("Task raised exception: " + t.Exception);
            });
        }

        private static async Task<bool> SendWebSocketMessageAsync(string message, CancellationTokenSource cts)
        {
            Task task = WebSocketClient.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)), WebSocketMessageType.Text, true, cts.Token);
            while (!(task.IsCompleted || cts.IsCancellationRequested))
                await Task.Delay(100);

            return !(task.IsFaulted || task.IsCanceled);
        }

        private static void OnAvatarReady(GameObject gameObject)
        {
            PiShockCVRMod.Logger.Msg("Local avatar instantiated. Scanning for PiShock objects...");

            Devices.Clear();

            FindPointsRecursively(gameObject.transform);
            if (Devices.Count == 0)
                PiShockCVRMod.Logger.Msg("No valid PiShockPoints were found on this avatar.");
        }

        public static void Update()
        {
            elapsedTime += Time.deltaTime;
            if (elapsedTime < (1f / Configuration.UpdateRate.Value))
                return;
            else
                elapsedTime = 0;

            if (!Configuration.Enabled.Value || Devices.Count == 0)
                return;

            if (Configuration.SelfInteraction.Value)
            {
                Animator animator = PlayerSetup.Instance._animator;
                if (animator != null && animator.isHuman)
                {
                    ProcessPlayer(animator.GetBoneTransform(HumanBodyBones.LeftHand)?.position ?? Vector3.positiveInfinity,
                        animator.GetBoneTransform(HumanBodyBones.RightHand)?.position ?? Vector3.positiveInfinity,
                        animator.GetBoneTransform(HumanBodyBones.LeftFoot)?.position ?? Vector3.positiveInfinity,
                        animator.GetBoneTransform(HumanBodyBones.RightFoot)?.position ?? Vector3.positiveInfinity,
                        MetaPort.Instance.username);
                }
            }

            foreach (CVRPlayerEntity player in CVRPlayerManager.Instance.NetworkPlayers)
            {
                if (player == null || player.PlayerDescriptor.avatarBlocked
                    || (Configuration.FriendsOnly.Value && !Friends.FriendsWith(player.Uuid)))
                    continue;

                ProcessPlayer(player.PuppetMaster.avatarLeftHandPosition,
                    player.PuppetMaster.avatarRightHandPosition,
                    player.PuppetMaster.avatarLeftFootPosition,
                    player.PuppetMaster.avatarRightFootPosition,
                    player.Username);
            }
        }

        private static void ProcessPlayer(Vector3 leftHand, Vector3 rightHand, Vector3 leftFoot, Vector3 rightFoot, string playerName)
        {
            foreach (PiShockDevice device in Devices)
            {
                if (device.NextValidShock >= Time.realtimeSinceStartup)
                    continue;

                foreach (PiShockPoint point in device.Points)
                {
                    if (point.Object == null || !point.Object.activeInHierarchy)
                        continue;

                    List<float> distances = new List<float>();

                    distances.Add(Vector3.Distance(leftHand, point.Object?.transform.position ?? Vector3.positiveInfinity));
                    distances.Add(Vector3.Distance(rightHand, point.Object?.transform.position ?? Vector3.positiveInfinity));

                    if (Configuration.FeetInteraction.Value)
                    {
                        distances.Add(Vector3.Distance(leftFoot, point.Object?.transform.position ?? Vector3.positiveInfinity));
                        distances.Add(Vector3.Distance(rightFoot, point.Object?.transform.position ?? Vector3.positiveInfinity));
                    }

                    float activationDistance = point.Radius ?? Configuration.DefaultRadius.Value;

                    if (distances.Any(distance => distance <= activationDistance))
                    {
                        device.NextValidShock = Time.realtimeSinceStartup + (point.Duration ?? Configuration.DefaultDuration.Value);
                        TriggerShock(device, point, playerName);
                        break;
                    }
                }
            }
        }

        private static void TriggerShock(PiShockDevice device, PiShockPoint point, string playerName)
        {
            if (Configuration.UseLocalServer.Value)
                SendLocalRequest(device, point);
            else
                SendAPIRequest(device, point, playerName);

            if (Configuration.UseAvatarParameters.Value)
            {
                ParameterController.SetParameter("PiShock" + device.Name, true);
                PiShockCVRMod.Run(() => ParameterController.SetParameter("PiShock" + device.Name, false), point.Duration ?? Configuration.DefaultDuration.Value);
            }
        }

        private static void FindPointsRecursively(Transform transform)
        {
            if (transform.name.StartsWith("PiShockPoint"))
            {
                transform = transform.Find("Settings");

                string identifier = transform.GetChild(0).name.Split(':')[1].Trim();

                PiShockDevice device = Devices.Where(d => d.Name.Equals(identifier)).DefaultIfEmpty(null).First();
                if (device == null)
                {
                    if (Configuration.DeviceLinks.TryGetValue(identifier, out PiShockDevice.LinkData linkData))
                    {
                        if (string.IsNullOrEmpty(linkData.ShareCode) && linkData.DeviceId < 0)
                        {
                            PiShockCVRMod.Logger.Warning("Found PiShock device without an assigned share code or local id [Identifier=" + identifier + "]. The device wont work.");
                        }
                        else
                        {
                            device = new PiShockDevice(identifier, linkData);
                            Devices.Add(device);
                            PiShockCVRMod.Logger.Msg("Found PiShock device [Identifier=" + identifier + "]");
                        }
                    }
                    else
                    {
                        Configuration.DeviceLinks.Add(identifier, new PiShockDevice.LinkData() { ShareCode = "", DeviceId = -1 });
                        Configuration.Save();
                        PiShockCVRMod.Logger.Msg("Found new PiShock device [Identifier=" + identifier + "]. Please assign a share code or local id in the configuration file.");
                    }
                }

                if (device != null)
                {
                    string rawType = transform.GetChild(1).name.Split(':')[1].Trim();
                    PiShockPoint.PointType? type = rawType.ToLower().Equals("default") ? null : (PiShockPoint.PointType)Enum.Parse(typeof(PiShockPoint.PointType), rawType);

                    string rawStrength = transform.GetChild(2).name.Split(':')[1].Trim();
                    int? strength = rawStrength.ToLower().Equals("default") ? null : int.Parse(rawStrength, CultureInfo.InvariantCulture);

                    string rawDuration = transform.GetChild(3).name.Split(':')[1].Trim();
                    int? duration = rawDuration.ToLower().Equals("default") ? null : int.Parse(rawDuration, CultureInfo.InvariantCulture);

                    float? radius = null;
                    if (transform.childCount >= 5)
                    {
                        string rawRadius = transform.GetChild(4).name.Split(':')[1].Trim();
                        radius = rawRadius.ToLower().Equals("default") ? null : float.Parse(rawRadius, CultureInfo.InvariantCulture);
                    }

                    device.Points.Add(new PiShockPoint() { Object = transform.parent.gameObject, Type = type, Strength = strength, Duration = duration, Radius = radius });

                    PiShockCVRMod.Logger.Msg("Found PiShockPoint [DeviceName=" + identifier + "/Type=" + (type == null ? "Default" : type.ToString()) + "/Strength=" + (strength == null ? "Default" : strength) + "/Duration=" + (duration == null ? "Default" : duration) + "/Radius=" + (radius == null ? "Default" : radius) + "]");
                }
            }
            else
            {
                foreach (Transform child in transform)
                    FindPointsRecursively(child);
            }
        }
    }
}
