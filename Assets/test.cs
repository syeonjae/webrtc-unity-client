using System;

using UnityEngine;
using Unity.WebRTC;
using UnityEngine.UI;

using WebSocketSharp;

using LitJson;
using System.Text;
using System.Collections.Generic;


internal static class WebRTCSettings
{
    public const int DefaultStreamWidth = 1280;
    public const int DefaultStreamHeight = 720;

    private static bool s_limitTextureSize = true;
    private static Vector2Int s_StreamSize = new Vector2Int(DefaultStreamWidth, DefaultStreamHeight);
    private static RTCRtpCodecCapability s_useVideoCodec = null;

    public static bool LimitTextureSize
    {
        get { return s_limitTextureSize; }
        set { s_limitTextureSize = value; }
    }

    public static Vector2Int StreamSize
    {
        get { return s_StreamSize; }
        set { s_StreamSize = value; }
    }

    public static RTCRtpCodecCapability UseVideoCodec
    {
        get { return s_useVideoCodec; }
        set { s_useVideoCodec = value; }
    }
}

[System.Serializable]
public class Data
{
    public string type;
    public string name;
    public string room;
    public bool success;
    public RTCSessionDescription offer;
    public RTCSessionDescription answer;
    public RTCIceCandidate candidate;
    public string mid;

    public override string ToString()
    {
        return $"Data.ToString()\n" +
                $"\ntype: {type}\n" +
                $"name: {name}\n" +
                $"room: {room}\n" +
                $"success: {success}\n" +
                $"offer.type: {offer.type}\n" +
                $"offer.sdp: {offer.sdp}\n" +
                $"answer.type: {answer.type}\n" +
                $"answer.sdp: {answer.sdp}\n" +
                $"candidate: {candidate}\n" +
                $"mid: {mid}";
    }
}


public class test : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private RawImage sourceImage;
    [SerializeField] private Transform rotateObject;

    private DelegateOnIceCandidate connIceCandidate;

    // Start is called before the first frame update
    private string IP = "192.168.1.143";
    private string PORT = "8080";

    public WebSocket ws = null;

    private RTCPeerConnection pc;
    private MediaStream videoStream = null;
    private string target; // 타겟이 있다면 항상 admin에게


    private static RTCConfiguration pc_config()
    {
        RTCConfiguration config = default;
        config.iceServers = new[] {
            new RTCIceServer {
            urls = new [] {"turn:183.96.152.34" },
            credential = "fakeeyes0906",
            username = "feturn"
            }};
        return config;
    }

    private void Awake()
    {
        WebRTC.Initialize(WebRTCSettings.LimitTextureSize);
    }

    private void OnDestroy()
    {
        WebRTC.Dispose();
    }

    private void Update()
    {
        if (rotateObject != null)
        {
            float t = Time.deltaTime;
            rotateObject.Rotate(100 * t, 200 * t, 300 * t);
        }
    }

    private Data onLogin()
    {
        Data data = new Data();
        data.type = "join_room";
        data.name = "user";
        data.room = "test";
        data.mid = "0001";
        return data;
    }
    void Start()
    {
        try
        {
            ws = new WebSocket("ws://" + IP + ":" + PORT);
            ws.OnMessage += Recv;
            ws.OnClose += CloseConnection;
            ws.Connect();
            SendTo(onLogin());
        }
        catch (Exception)
        {
            throw;
        }
    }

    public void Connect()
    {
        try
        {
            if (ws == null || !ws.IsAlive)
            {
                ws.Close();
            }
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
        }
    }

    private void CloseConnection(object sender, CloseEventArgs e)
    {
        Data data = new Data();
        data.type = "leave";
        data.room = "test";
        data.name = "user";
        target = "";
        SendTo(data);
        DisconnectServer();
    }

    public void DisconnectServer()
    {
        Debug.Log("DisconnectServer");
        try
        {
            if (ws == null)
            {
                return;
            }

            if (ws.IsAlive)
            {
                pc.Close();
                pc.Dispose();
                pc = null;
            }
        }
        catch (Exception)
        {
            throw;
        }
    }



    public void Recv(object sender, MessageEventArgs e)
    {
        try
        {
            Data recvData = JsonUtility.FromJson<Data>(e.Data);
            Debug.Log($"Recv.Data: {e.Data}");
            Debug.Log($"recvData: {recvData}");

            switch (recvData.type)
            {
                case "login":
                    handleLogin(recvData.success);
                    break;
                case "offer":
                    RTCSessionDescription offer = new RTCSessionDescription()
                    {
                        type = RTCSdpType.Offer,
                        sdp = JsonMapper.ToObject(e.Data)["offer"]["sdp"].ToString(),
                    };
                    handleOffer(offer, recvData.name);
                    break;
                case "answer":
                    handleAnswer(recvData.answer);
                    break;
                case "candidate":
                    JsonData data = JsonMapper.ToObject(e.Data)["candidate"];
                    string candidate = data[nameof(candidate)].ToString();
                    string sdpMid = data[nameof(sdpMid)].ToString();
                    int sdpMLineIndex = int.Parse(data[nameof(sdpMLineIndex)].ToString());

                    RTCIceCandidate _candidate = new RTCIceCandidate(new RTCIceCandidateInit()
                    {
                        candidate = candidate,
                        sdpMid = sdpMid,
                        sdpMLineIndex = sdpMLineIndex,
                    });
                    handleCandidate(_candidate);
                    
                    break;
                case "leave":
                    handleLeave();
                    break;
            }
        }
        catch (Exception error)
        {
            Debug.LogError($"Message: {error.Message}\nStackTrace: {error.StackTrace}");
            throw error;
        }
    }

    public async void handleLogin(bool success)
    {
        if (!success)
        {
            Debug.Log("로그인 실패!! 다시 시도해주세요");
        }
        else
        {

            
            Debug.Log("로그인 성공");
            var configuration = pc_config();

            pc = new RTCPeerConnection(ref configuration);
            target = "user";
            videoStream = null;
            MainThreadHelper.AddAction(() =>
            {
                videoStream = cam.CaptureStream(WebRTCSettings.StreamSize.x, WebRTCSettings.StreamSize.y, 0);
            });

            await System.Threading.Tasks.Task.Run(() =>
            {
                while (true)
                {
                    if (videoStream != null)
                        break;
                }
            });

            Debug.Log($"find videoStream finished");


            // add Track
            var pcVideoSenders = new List<RTCRtpSender>();
            foreach (var track in videoStream.GetTracks())
            {
                var pcSender = pc.AddTrack(track, videoStream);

                if (track.Kind == TrackKind.Video)
                {
                    pcVideoSenders.Add(pcSender);
                }
            }

            if (WebRTCSettings.UseVideoCodec != null)
            {
                var codecs = new[] { WebRTCSettings.UseVideoCodec };
                foreach (var transceiver in pc.GetTransceivers())
                {
                    if (pcVideoSenders.Contains(transceiver.Sender))
                    {
                        transceiver.SetCodecPreferences(codecs);
                    }
                }
            }
            sourceImage.texture = cam.targetTexture;


            pc.OnIceCandidate = candidate =>
            {
                try
                {
                    Debug.Log("유저가 어드민에게 icecandidate 전달");
                    target = "admin";
                    RTCIceCandidate _candidate = new RTCIceCandidate(new RTCIceCandidateInit()
                    {
                        candidate = candidate.Candidate,
                        sdpMid = candidate.SdpMid,
                        sdpMLineIndex = candidate.SdpMLineIndex,
                    });

                    Data data = new Data();
                    data.type = "candidate";
                    data.room = "test";
                    data.candidate = _candidate;
                    SendTo(data);
                }
                catch (Exception error)
                {
                    Debug.Log("OnIceCandidate");
                    Debug.Log(error);
                }
            };

        }
        Debug.Log("handleLogin 종료");

    }

    public async void handleOffer(RTCSessionDescription offer, string name)
    {
        target = name;
        pc.SetRemoteDescription(ref offer);

        var answer = pc.CreateAnswer();
        await System.Threading.Tasks.Task.Run(() =>
        {
            while (true)
            {
                if (answer.IsDone)
                {
                    Debug.Log($"유저 answer 생성 완료");
                    break;
                }
            }
        });
        onCreateAnswerSuccess(pc, answer.Desc);
    }

    void onCreateAnswerSuccess(RTCPeerConnection pc, RTCSessionDescription answer)
    {
        pc.SetLocalDescription(ref answer);
        target = "admin";
        RTCSessionDescription _answer = new RTCSessionDescription()
        {
            sdp = answer.sdp,
            type = answer.type,
        };

        Data data = new Data();
        data.answer = _answer;
        data.name = "admin";
        data.room = "test";
        data.type = "answer";

        string jsonData = JsonMapper.ToJson(data);
        ws.Send(jsonData);
        Debug.Log("answer을 어드민에게 전달");

        pc.OnIceCandidate = candidate =>
        {
            try
            {
                Debug.Log("유저가 어드민에게 icecandidate 전달");
                RTCIceCandidate _candidate = new RTCIceCandidate(new RTCIceCandidateInit()
                {
                    candidate = candidate.Candidate,
                    sdpMid = candidate.SdpMid,
                    sdpMLineIndex = candidate.SdpMLineIndex,
                });

                //Data data = new Data();
                //data.type = "candidate";
                //data.room = "test";
                //data.name = "admin";
                //data.candidate = _candidate;
                //Debug.Log("data.candidate");
                //Debug.Log(data.candidate);
                //SendTo(data);

                string json = "{ \"type\": \"candidate\", \"name\": \"admin\", \"room\": \"test\", \"candidate\" : \" \" }";
                SendToJson(json);
            }
            catch (Exception error)
            {
                Debug.Log("OnIceCandidate");
                Debug.Log(error);
            }
        };
    }

    public void handleAnswer(RTCSessionDescription answer)
    {
        Debug.Log("handleAnswer");
    }

    public void handleCandidate(RTCIceCandidate candidate)
    {
        pc.AddIceCandidate(candidate);
       Debug.Log("유저가 ICE candidate 등록");
    }

    public void handleLeave()
    {
        Debug.Log("handleLeave");
        //
    }


    public void SendTo(Data message)
    {
        Debug.Log($"SendTo. message: {message.name}");
        if (!ws.IsAlive)
        {
            return;
        }
        try
        {
            if(target == "")
            {
                message.name = "admin";
            }
            string msg = JsonUtility.ToJson(message);
            Debug.Log(msg);
            ws.Send(msg);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public void SendToJson(string message)
    {
        try
        {
            ws.Send(message);
        }
        catch (Exception)
        {
            Debug.LogError("Invalid JSON");
            throw;
        }
    }

}
