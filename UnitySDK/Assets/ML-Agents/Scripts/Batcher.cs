﻿using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Linq;
using UnityEngine;
using Google.Protobuf;
using MLAgents.CommunicatorObjects;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;

namespace MLAgents
{
    /// <summary>
    /// The batcher is an RL specific class that makes sure that the information each object in 
    /// Unity (Academy and Brains) wants to send to External is appropriately batched together 
    /// and sent only when necessary.
    /// 
    /// The Batcher will only send a Message to the Communicator when either :
    ///     1 - The academy is done
    ///     2 - At least one brain has data to send
    /// 
    /// At each step, the batcher will keep track of the brains that queried the batcher for that 
    /// step. The batcher can only send the batched data when all the Brains have queried the
    /// Batcher.
    /// </summary>
    public class Batcher
    {
        public static int count = 0;
        /// The default number of agents in the scene
        private const int NumAgents = 32;

        /// Keeps track of which brains have data to send on the current step
        Dictionary<string, bool> m_hasData =
            new Dictionary<string, bool>();
        /// Keeps track of which brains queried the batcher on the current step
        Dictionary<string, bool> m_hasQueried =
            new Dictionary<string, bool>();
        /// Keeps track of the agents of each brain on the current step
        Dictionary<string, List<Agent>> m_currentAgents =
            new Dictionary<string, List<Agent>>();
        /// The Communicator of the batcher, sends a message at most once per step
        Communicator m_communicator;
        /// The current UnityRLOutput to be sent when all the brains queried the batcher
        CommunicatorObjects.UnityRLOutput m_currentUnityRLOutput =
                               new CommunicatorObjects.UnityRLOutput();
        /// Keeps track of the done flag of the Academy
        bool m_academyDone;
        /// Keeps track of last CommandProto sent by External
        CommunicatorObjects.CommandProto m_command;
        /// Keeps track of last EnvironmentParametersProto sent by External
        CommunicatorObjects.EnvironmentParametersProto m_environmentParameters;
        /// Keeps track of last training mode sent by External
        bool m_isTraining;

        /// Keeps track of the number of messages received
        private ulong m_messagesReceived;

        /// <summary>
        /// Initializes a new instance of the Batcher class.
        /// </summary>
        /// <param name="communicator">The communicator to be used by the batcher.</param>
        public Batcher(Communicator communicator)
        {
            this.m_communicator = communicator;
        }

        /// <summary>
        /// Sends the academy parameters through the Communicator. 
        /// Is used by the academy to send the AcademyParameters to the communicator.
        /// </summary>
        /// <returns>The External Initialization Parameters received.</returns>
        /// <param name="academyParameters">The Unity Initialization Paramters to be sent.</param>
        public CommunicatorObjects.UnityRLInitializationInput SendAcademyParameters(
            CommunicatorObjects.UnityRLInitializationOutput academyParameters)
        {
            CommunicatorObjects.UnityInput input;
            var initializationInput = new CommunicatorObjects.UnityInput();
            try
            {
                initializationInput = m_communicator.Initialize(
                        new CommunicatorObjects.UnityOutput
                        {
                            RlInitializationOutput = academyParameters
                        },
                        out input);
            }
            catch
            {
                throw new UnityAgentsException(
                    "The Communicator was unable to connect. Please make sure the External " +
                    "process is ready to accept communication with Unity.");
            }

            var firstRlInput = input.RlInput;
            m_command = firstRlInput.Command;
            m_environmentParameters = firstRlInput.EnvironmentParameters;
            m_isTraining = firstRlInput.IsTraining;
            return initializationInput.RlInitializationInput;
        }

        /// <summary>
        /// Registers the done flag of the academy to the next output to be sent
        /// to the communicator.
        /// </summary>
        /// <param name="done">If set to <c>true</c> 
        /// The academy done state will be sent to External at the next Exchange.</param>
        public void RegisterAcademyDoneFlag(bool done)
        {
            m_academyDone = done;
        }

        /// <summary>
        /// Gets the command. Is used by the academy to get reset or quit signals.
        /// </summary>
        /// <returns>The current command.</returns>
        public CommunicatorObjects.CommandProto GetCommand()
        {
            return m_command;
        }

        /// <summary>
        /// Gets the number of messages received so far. Can be used to check for new messages.
        /// </summary>
        /// <returns>The number of messages received since start of the simulation</returns>
        public ulong GetNumberMessageReceived()
        {
            return m_messagesReceived;
        }

        /// <summary>
        /// Gets the environment parameters. Is used by the academy to update
        /// the environment parameters.
        /// </summary>
        /// <returns>The environment parameters.</returns>
        public CommunicatorObjects.EnvironmentParametersProto GetEnvironmentParameters()
        {
            return m_environmentParameters;
        }

        /// <summary>
        /// Gets the last training_mode flag External sent
        /// </summary>
        /// <returns><c>true</c>, if training mode is requested, <c>false</c> otherwise.</returns>
        public bool GetIsTraining()
        {
            return m_isTraining;
        }

        /// <summary>
        /// Adds the brain to the list of brains which will be sending information to External.
        /// </summary>
        /// <param name="brainKey">Brain key.</param>
        public void SubscribeBrain(string brainKey)
        {
            m_hasQueried[brainKey] = false;
            m_hasData[brainKey] = false;
            m_currentAgents[brainKey] = new List<Agent>(NumAgents);
            m_currentUnityRLOutput.AgentInfos.Add(
                brainKey,
                new CommunicatorObjects.UnityRLOutput.Types.ListAgentInfoProto());
        }

        /// <summary>
        /// Converts a AgentInfo to a protobuffer generated AgentInfoProto
        /// </summary>
        /// <returns>The protobuf verison of the AgentInfo.</returns>
        /// <param name="info">The AgentInfo to convert.</param>
        public static CommunicatorObjects.AgentInfoProto 
                                         AgentInfoConvertor(AgentInfo info)
        {

            var agentInfoProto = new CommunicatorObjects.AgentInfoProto
            {
                StackedVectorObservation = { info.stackedVectorObservation },
                StoredVectorActions = { info.storedVectorActions },
                StoredTextActions = info.storedTextActions,
                TextObservation = info.textObservation,
                Reward = info.reward,
                MaxStepReached = info.maxStepReached,
                Done = info.done,
                Id = info.id,
            };
            if (info.memories != null)
            {
                agentInfoProto.Memories.Add(info.memories);
            }
            if (info.actionMasks != null)
            {
                agentInfoProto.ActionMask.AddRange(info.actionMasks);
            }

            int i = 1;
            Mat image = new Mat();
            Mat trimap = new Mat();
            foreach (Texture2D obs in info.visualObservations)
            {
                
                //Color32[] texDataColor = obs.GetPixels32();
                //Pin Memory
                //fixed (Color32* p = texDataColor)
                //{
                   //TextureToCVMat((IntPtr)p, obs.width, obs.height);
                //}
                /*agentInfoProto.VisualObservations.Add(
                    ByteString.CopyFrom(obs.GetRawTextureData())
                );*/
                //obs.GetRawTextureData()
                //var fileBytes= ByteString.CopyFrom(obs.EncodeToPNG()).ToByteArray();
                /*using (Stream file = File.OpenWrite(@"d:\here.txt"))
                {
                    file.Write(fileBytes, 0, fileBytes.Length);
                }*/
                if (i == 1)
                {
                    var bytes = obs.GetPixels32();
                    image = UnityTextureToOpenCVImage(bytes, obs.width, obs.height);
                    i++;
                }
                else if (i == 2)
                {
                    var bytes = obs.GetPixels32();
                    trimap = UnityTextureToOpenCVImage(bytes, obs.width, obs.height);
                    var one_channel = trimap.Split()[0];
                    Mat alpha = new Mat(obs.width, obs.height, DepthType.Cv8U, 1);
                    alpha.SetTo(new MCvScalar(0));
                    alpha.SetTo(new MCvScalar(255), one_channel);
                    CvInvoke.Resize(alpha, alpha, new Size(), 0.5, 0.5, Inter.Area);
                    CvInvoke.Resize(image, image, new Size(), 0.5, 0.5, Inter.Area);
                    var channels = image.Split();
                    Mat result = new Mat();
                    CvInvoke.Merge(new VectorOfMat(channels[0], channels[1], channels[2], alpha), result);
                    CvInvoke.Imwrite("debug/result_" + count.ToString() + ".png", result);
                    count++;

                    i = 1;
                    var image_bytes = result.ToImage<Bgra, byte>();
                    agentInfoProto.VisualObservations.Add(ByteString.CopyFrom(image_bytes.Bytes));
                }
                //var bytes = obs.GetPixels32();
                //var output = UnityTextureToOpenCVImage(bytes, obs.width, obs.height);
                //output.Save("image.png");
            }
            return agentInfoProto;
        }
        
        public static Mat UnityTextureToOpenCVImage(Color32[] data, int width, int height){ 

            byte[,,] imageData = new byte[width, height, 3]; 


            int index = 0; 
            for (int y = height - 1 ; y > -1; y--) { 
                for (int x = width - 1; x > -1; x--) { 
                    imageData[y,x,0] = data[index].b; 
                    imageData[y,x,1] = data[index].g; 
                    imageData[y,x,2] = data[index].r; 

                    index++; 
                } 
            } 

            Image<Bgr, byte> image = new Image<Bgr, byte>(imageData);
            var img = image.Flip(Emgu.CV.CvEnum.FlipType.Horizontal);

            return img.Mat;
        } 

        /// <summary>
        /// Converts a Brain into to a Protobuff BrainInfoProto so it can be sent
        /// </summary>
        /// <returns>The BrainInfoProto generated.</returns>
        /// <param name="brainParameters">The BrainParameters.</param>
        /// <param name="name">The name of the brain.</param>
        /// <param name="type">The type of brain.</param>
        public static CommunicatorObjects.BrainParametersProto BrainParametersConvertor(
            BrainParameters brainParameters, string name, CommunicatorObjects.BrainTypeProto type)
        {

            var brainParametersProto = new CommunicatorObjects.BrainParametersProto
                {
                    VectorObservationSize = brainParameters.vectorObservationSize,
                    NumStackedVectorObservations = brainParameters.numStackedVectorObservations,
                    VectorActionSize = {brainParameters.vectorActionSize},
                    VectorActionSpaceType =
                    (CommunicatorObjects.SpaceTypeProto)brainParameters.vectorActionSpaceType,
                    BrainName = name,
                    BrainType = type
                };
            brainParametersProto.VectorActionDescriptions.AddRange(
                brainParameters.vectorActionDescriptions);
            foreach (resolution res in brainParameters.cameraResolutions)
            {
                brainParametersProto.CameraResolutions.Add(
                    new CommunicatorObjects.ResolutionProto
                    {
                        Width = res.width,
                        Height = res.height,
                        GrayScale = res.blackAndWhite
                    });
            }
            return brainParametersProto;
        }

        /// <summary>
        /// Sends the brain info. If at least one brain has an agent in need of
        /// a decision or if the academy is done, the data is sent via 
        /// Communicator. Else, a new step is realized. The data can only be
        /// sent once all the brains that subscribed to the batcher have tried
        /// to send information.
        /// </summary>
        /// <param name="brainKey">Brain key.</param>
        /// <param name="agentInfo">Agent info.</param>
        public void SendBrainInfo(
            string brainKey, Dictionary<Agent, AgentInfo> agentInfo)
        {
            // If no communicator is initialized, the Batcher will not transmit
            // BrainInfo
            if (m_communicator == null)
            {
                return;
            }

            // The brain tried called GiveBrainInfo, update m_hasQueried
            m_hasQueried[brainKey] = true;
            // Populate the currentAgents dictionary
            m_currentAgents[brainKey].Clear();
            foreach (Agent agent in agentInfo.Keys)
            {
                m_currentAgents[brainKey].Add(agent);
            }
            // If at least one agent has data to send, then append data to
            // the message and update hasSentState
            if (m_currentAgents[brainKey].Count > 0)
            {
                foreach (Agent agent in m_currentAgents[brainKey])
                {
                    CommunicatorObjects.AgentInfoProto agentInfoProto =
                        AgentInfoConvertor(agentInfo[agent]);
                    m_currentUnityRLOutput.AgentInfos[brainKey].Value.Add(agentInfoProto);
                }
                m_hasData[brainKey] = true;
            }

            // If any agent needs to send data, then the whole message
            // must be sent
            if (m_hasQueried.Values.All(x => x))
            {
                if (m_hasData.Values.Any(x => x) || m_academyDone)
                {
                    m_currentUnityRLOutput.GlobalDone = m_academyDone;
                    SendBatchedMessageHelper();
                }
                // The message was just sent so we must reset hasSentState and
                // triedSendState
                foreach (string k in m_currentAgents.Keys)
                {
                    m_hasData[k] = false;
                    m_hasQueried[k] = false;
                }
            }
        }
        
        public Dictionary<string, global::MLAgents.CommunicatorObjects.UnityRLOutput.Types.ListAgentInfoProto> collectData(string brainKey,
            Dictionary<Agent, AgentInfo> agentInfo)
        {
            // The brain tried called GiveBrainInfo, update m_hasQueried
            m_hasQueried[brainKey] = true;
            // Populate the currentAgents dictionary
            m_currentAgents[brainKey].Clear();
            foreach (Agent agent in agentInfo.Keys)
            {
                m_currentAgents[brainKey].Add(agent);
            }
            // If at least one agent has data to send, then append data to
            // the message and update hasSentState
            Dictionary<string, global::MLAgents.CommunicatorObjects.UnityRLOutput.Types.ListAgentInfoProto> AgentInfos = new Dictionary<string, UnityRLOutput.Types.ListAgentInfoProto>();
            AgentInfos.Add(
                brainKey,
                new CommunicatorObjects.UnityRLOutput.Types.ListAgentInfoProto());
            if (m_currentAgents[brainKey].Count > 0)
            {
                foreach (Agent agent in m_currentAgents[brainKey])
                {
                    CommunicatorObjects.AgentInfoProto agentInfoProto =
                        AgentInfoConvertor(agentInfo[agent]);
                    AgentInfos[brainKey].Value.Add(agentInfoProto);
                }
                //m_hasData[brainKey] = true;
            }

            return AgentInfos;
        }

        /// <summary>
        /// Helper method that sends the curent UnityRLOutput, receives the next UnityInput and
        /// Applies the appropriate AgentAction to the agents.
        /// </summary>
        void SendBatchedMessageHelper()
        {
            var input = m_communicator.Exchange(
                new CommunicatorObjects.UnityOutput{
                RlOutput = m_currentUnityRLOutput
            });
            m_messagesReceived += 1;

            foreach (string k in m_currentUnityRLOutput.AgentInfos.Keys)
            {
                m_currentUnityRLOutput.AgentInfos[k].Value.Clear();
            }
            if (input == null)
            {
                m_command = CommunicatorObjects.CommandProto.Quit;
                return;
            }

            CommunicatorObjects.UnityRLInput rlInput = input.RlInput;

            if (rlInput == null)
            {
                m_command = CommunicatorObjects.CommandProto.Quit;
                return;
            }

            m_command = rlInput.Command;
            m_environmentParameters = rlInput.EnvironmentParameters;
            m_isTraining = rlInput.IsTraining;

            if (rlInput.AgentActions == null)
            {
                return;
            }

            foreach (var brainName in rlInput.AgentActions.Keys)
                {
                    if (!m_currentAgents[brainName].Any())
                    {
                        continue;
                    }
                    if (!rlInput.AgentActions[brainName].Value.Any())
                    {
                        continue;
                    }
                    for (var i = 0; i < m_currentAgents[brainName].Count(); i++)
                    {
                        var agent = m_currentAgents[brainName][i];
                        var action = rlInput.AgentActions[brainName].Value[i];
                        agent.UpdateVectorAction(
                            action.VectorActions.ToArray());
                        agent.UpdateMemoriesAction(
                            action.Memories.ToList());
                        agent.UpdateTextAction(
                            action.TextActions);
                        agent.UpdateValueAction(
                            action.Value);
                    }
                }
            
        }

    }
}



