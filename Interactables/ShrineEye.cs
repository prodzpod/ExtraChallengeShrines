using UnityEngine;
using MysticsRisky2Utils;
using RoR2;
using RoR2.Skills;
using UnityEngine.Networking;
using MysticsRisky2Utils.BaseAssetTypes;
using MysticsRisky2Utils.ContentManagement;
using R2API;
using RoR2.Projectile;
using RoR2.Navigation;
using RoR2.Networking;
using UnityEngine.Events;
using UnityEngine.AddressableAssets;
using RoR2.UI;
using UnityEngine.UI;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using R2API.Networking.Interfaces;
using R2API.Networking;
using System.Linq;

namespace ExtraChallengeShrines.Interactables
{
    public class ShrineEye : BlankLoadableAsset
    {
        public static GameObject shrinePrefab;
        public static InteractableSpawnCard spawnCard;

        public static ConfigOptions.ConfigurableValue<float> bossCredits = ConfigOptions.ConfigurableValue.CreateFloat(
            ExtraChallengeShrinesPlugin.PluginGUID,
            ExtraChallengeShrinesPlugin.PluginName,
            ExtraChallengeShrinesPlugin.config,
            "Shrine of the Wind",
            "Boss Credits",
            300f,
            0f,
            100000f,
            "How many director credits to add when this shrine is first used?",
            useDefaultValueConfigEntry: ExtraChallengeShrinesPlugin.ignoreBalanceChanges
        );
        public static ConfigOptions.ConfigurableValue<float> bossCreditsPerStack = ConfigOptions.ConfigurableValue.CreateFloat(
            ExtraChallengeShrinesPlugin.PluginGUID,
            ExtraChallengeShrinesPlugin.PluginName,
            ExtraChallengeShrinesPlugin.config,
            "Shrine of the Wind",
            "Boss Credits Per Stack",
            600f,
            0f,
            100000f,
            "How many director credits to add for each time this shrine is used more than once?",
            useDefaultValueConfigEntry: ExtraChallengeShrinesPlugin.ignoreBalanceChanges
        );
        public static ConfigOptions.ConfigurableValue<float> yellowChance = ConfigOptions.ConfigurableValue.CreateFloat(
            ExtraChallengeShrinesPlugin.PluginGUID,
            ExtraChallengeShrinesPlugin.PluginName,
            ExtraChallengeShrinesPlugin.config,
            "Shrine of the Wind",
            "Yellow Chance",
            25f,
            0f,
            100f,
            "How much to increase the yellow item drop chance?",
            useDefaultValueConfigEntry: ExtraChallengeShrinesPlugin.ignoreBalanceChanges
        );
        public static ConfigOptions.ConfigurableValue<bool> yellowChanceStackingReduction = ConfigOptions.ConfigurableValue.CreateBool(
            ExtraChallengeShrinesPlugin.PluginGUID,
            ExtraChallengeShrinesPlugin.PluginName,
            ExtraChallengeShrinesPlugin.config,
            "Shrine of the Wind",
            "Yellow Chance Stacking Reduction",
            true,
            "Should each additional use of this shrine add less yellow item drop chance than the previous use? For example, the first shrine will add +25%, the next one - +25%/2, the next one - +25%/3 and so on.",
            useDefaultValueConfigEntry: ExtraChallengeShrinesPlugin.ignoreBalanceChanges
        );

        public override void OnPluginAwake()
        {
            base.OnPluginAwake();
            NetworkingAPI.RegisterMessageType<ExtraChallengeShrinesShrineEyeBehaviour.SyncBodyOptions>();
        }

        public override void OnLoad()
        {
            base.OnLoad();

            shrinePrefab = ExtraChallengeShrinesPlugin.AssetBundle.LoadAsset<GameObject>("Assets/Mods/ExtraChallengeShrines/ShrineEye/ShrineEye.prefab");

            foreach (Renderer renderer in shrinePrefab.GetComponentsInChildren<Renderer>())
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null && material.shader.name == "Standard" && material.shader != HopooShaderToMaterial.Standard.shader)
                    {
                        HopooShaderToMaterial.Standard.Apply(material);
                        HopooShaderToMaterial.Standard.Dither(material);
                    }
                }
            }

            var existingSymbol = LegacyResourcesAPI.Load<GameObject>("Prefabs/NetworkedObjects/Shrines/ShrineBoss").transform.Find("Symbol").gameObject;
            var symbol = shrinePrefab.transform.Find("Symbol").gameObject;
            symbol.GetComponent<MeshFilter>().mesh = Object.Instantiate(existingSymbol.GetComponent<MeshFilter>().mesh);
            Material symbolMaterial = Object.Instantiate(existingSymbol.GetComponent<MeshRenderer>().material);
            symbol.GetComponent<MeshRenderer>().material = symbolMaterial;
            symbolMaterial.SetTexture("_MainTex", ExtraChallengeShrinesPlugin.AssetBundle.LoadAsset<Texture>("Assets/Mods/ExtraChallengeShrines/ShrineEye/Symbol.png"));
            symbolMaterial.SetColor("_TintColor", new Color32(249, 255, 96, 255));

            ExtraChallengeShrinesTeleporterComponent.eyeShrineIndicatorMaterial = symbolMaterial;

            var shrineBehaviour = shrinePrefab.AddComponent<ExtraChallengeShrinesShrineEyeBehaviour>();
            shrineBehaviour.symbolTransform = symbol.transform;

            shrineBehaviour.networkUIPromptController = shrinePrefab.AddComponent<NetworkUIPromptController>();

            var pickerPanelPrefab = PrefabAPI.InstantiateClone(Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Command/CommandPickerPanel.prefab").WaitForCompletion(), "ShrineEyePickerPanel", true);
            var pickupPickerPanel = pickerPanelPrefab.GetComponent<PickupPickerPanel>();
            var bodyPickerPanel = pickerPanelPrefab.AddComponent<BodyPickerPanel>();
            bodyPickerPanel.buttonContainer = pickupPickerPanel.buttonContainer;
            bodyPickerPanel.buttonPrefab = pickupPickerPanel.buttonPrefab;
            bodyPickerPanel.coloredImages = pickupPickerPanel.coloredImages;
            bodyPickerPanel.darkColoredImages = pickupPickerPanel.darkColoredImages;
            bodyPickerPanel.gridlayoutGroup = pickupPickerPanel.gridlayoutGroup;
            bodyPickerPanel.maxColumnCount = pickupPickerPanel.maxColumnCount;
            Object.Destroy(pickupPickerPanel);
            pickerPanelPrefab.GetComponentsInChildren<LanguageTextMeshController>()[0]._token = "EXTRACHALLENGESHRINES_SHRINE_EYE_INTERACTION_HEADER";
            shrineBehaviour.pickerPanelPrefab = pickerPanelPrefab;

            ExtraChallengeShrinesContent.Resources.networkedObjectPrefabs.Add(shrinePrefab);

            spawnCard = ScriptableObject.CreateInstance<InteractableSpawnCard>();
            spawnCard.name = "iscExtraChallengeShrines_ShrineEye";
            spawnCard.prefab = shrinePrefab;
            spawnCard.sendOverNetwork = true;
            spawnCard.hullSize = HullClassification.Golem;
            spawnCard.nodeGraphType = MapNodeGroup.GraphType.Ground;
            spawnCard.requiredFlags = NodeFlags.None;
            spawnCard.forbiddenFlags = NodeFlags.NoShrineSpawn;
            spawnCard.directorCreditCost = 20;
            spawnCard.occupyPosition = true;
            spawnCard.orientToFloor = true;
            spawnCard.slightlyRandomizeOrientation = true;
            spawnCard.skipSpawnWhenSacrificeArtifactEnabled = false;

            ConfigOptions.ConfigurableValue.CreateInt(
                ExtraChallengeShrinesPlugin.PluginGUID,
                ExtraChallengeShrinesPlugin.PluginName,
                ExtraChallengeShrinesPlugin.config,
                "Shrine of the Wind",
                "Director Credit Cost",
                20,
                0,
                1000,
                onChanged: (x) => spawnCard.directorCreditCost = x
            );
            ConfigOptions.ConfigurableValue.CreateInt(
                ExtraChallengeShrinesPlugin.PluginGUID,
                ExtraChallengeShrinesPlugin.PluginName,
                ExtraChallengeShrinesPlugin.config,
                "Shrine of the Wind",
                "Max Spawns Per Stage",
                1,
                -1,
                1000,
                description: "-1 means no limit",
                onChanged: (x) => spawnCard.maxSpawnsPerStage = x
            );

            var directorCardRare = new DirectorCard
            {
                spawnCard = spawnCard,
                selectionWeight = 1,
                spawnDistance = 0f,
                preventOverhead = false,
                minimumStageCompletions = 0,
                requiredUnlockableDef = null,
                forbiddenUnlockableDef = null
            };
            var directorCardCommon = new DirectorCard
            {
                spawnCard = spawnCard,
                selectionWeight = 5,
                spawnDistance = 0f,
                preventOverhead = false,
                minimumStageCompletions = 0,
                requiredUnlockableDef = null,
                forbiddenUnlockableDef = null
            };

            var stageNames = ConfigOptions.ConfigurableValue.CreateString(
                ExtraChallengeShrinesPlugin.PluginGUID,
                ExtraChallengeShrinesPlugin.PluginName,
                ExtraChallengeShrinesPlugin.config,
                "Shrine of the Wind",
                "Stages",
                "frozenwall,wispgraveyard,rootjungle",
                restartRequired: true
            );
            foreach (var stageName in stageNames.Value.Split(','))
                BaseInteractable.AddDirectorCardTo(stageName, "Shrines", directorCardCommon);

            stageNames = ConfigOptions.ConfigurableValue.CreateString(
                ExtraChallengeShrinesPlugin.PluginGUID,
                ExtraChallengeShrinesPlugin.PluginName,
                ExtraChallengeShrinesPlugin.config,
                "Shrine of the Wind",
                "Stages (Rare)",
                "shipgraveyard,skymeadow,snowyforest",
                restartRequired: true
            );
            foreach (var stageName in stageNames.Value.Split(','))
                BaseInteractable.AddDirectorCardTo(stageName, "Shrines", directorCardRare);

            ConfigOptions.ConfigurableValue.CreateInt(
                ExtraChallengeShrinesPlugin.PluginGUID,
                ExtraChallengeShrinesPlugin.PluginName,
                ExtraChallengeShrinesPlugin.config,
                "Shrine of the Wind",
                "Selection Weight",
                5,
                0,
                1000,
                onChanged: (x) => {
                    directorCardCommon.selectionWeight = x;
                }
            );
            ConfigOptions.ConfigurableValue.CreateInt(
                ExtraChallengeShrinesPlugin.PluginGUID,
                ExtraChallengeShrinesPlugin.PluginName,
                ExtraChallengeShrinesPlugin.config,
                "Shrine of the Wind",
                "Selection Weight (Rare)",
                1,
                0,
                1000,
                onChanged: (x) => {
                    directorCardRare.selectionWeight = x;
                }
            );

            ConfigOptions.ConfigurableValue.CreateInt(
                ExtraChallengeShrinesPlugin.PluginGUID,
                ExtraChallengeShrinesPlugin.PluginName,
                ExtraChallengeShrinesPlugin.config,
                "Shrine of the Wind",
                "Minimum Stage Completions",
                0,
                0,
                99,
                description: "Need to clear this many stages before it can spawn",
                onChanged: (x) => {
                    directorCardCommon.minimumStageCompletions = x;
                    directorCardRare.minimumStageCompletions = x;
                }
            );

            SceneDirector.onGenerateInteractableCardSelection += SceneDirector_onGenerateInteractableCardSelection;
        }

        private void SceneDirector_onGenerateInteractableCardSelection(SceneDirector sceneDirector, DirectorCardCategorySelection dccs)
        {
            if (RunArtifactManager.instance &&
                RunArtifactManager.instance.IsArtifactEnabled(RoR2Content.Artifacts.singleMonsterTypeArtifactDef))
            {
                dccs.RemoveCardsThatFailFilter(x =>
                {
                    var prefab = x.spawnCard.prefab;
                    return !prefab.GetComponent<ExtraChallengeShrinesShrineEyeBehaviour>();
                });
            }
        }
    }

    public class ExtraChallengeShrinesShrineEyeBehaviour : NetworkBehaviour, IBodyPickerPanelSignalReceiver
    {
        public int maxPurchaseCount = 1;
        public float costMultiplierPerPurchase = 2f;
        public Transform symbolTransform;
        public PurchaseInteraction purchaseInteraction;
        public int purchaseCount;
        public float refreshTimer;
        public const float refreshDuration = 0.5f;
        public bool waitingForRefresh;

        public NetworkIdentity networkIdentity;

        public NetworkUIPromptController networkUIPromptController;
        public GameObject pickerPanelPrefab;

        private GameObject panelInstance;
        private BodyPickerPanel panelInstanceController;

        public List<BodyIndex> bodyOptions = new List<BodyIndex>();
        public float buildBodyOptionsAttemptTimer = 2f;
        public BodyIndex selectedBody = BodyIndex.None;

        public void Start()
        {
            networkIdentity = GetComponent<NetworkIdentity>();

            purchaseInteraction = GetComponent<PurchaseInteraction>();
            purchaseInteraction.onPurchase.AddListener((interactor) =>
            {
                if (bodyOptions.Count > 0)
                {
                    purchaseInteraction.SetAvailable(false);
                    if (TeleporterInteraction.instance)
                    {
                        var tpComponent = TeleporterInteraction.instance.GetComponent<ExtraChallengeShrinesTeleporterComponent>();
                        if (tpComponent && tpComponent.eyeSelectedBody == BodyIndex.None)
                        {
                            networkUIPromptController.SetParticipantMasterFromInteractor(interactor);
                            return;
                        }
                    }
                    AddShrineStack(interactor);
                }
            });

            if (NetworkClient.active)
            {
                networkUIPromptController.onDisplayBegin += OnDisplayBegin;
                networkUIPromptController.onDisplayEnd += OnDisplayEnd;
            }
            if (NetworkServer.active)
            {
                networkUIPromptController.messageFromClientHandler = new System.Action<NetworkReader>(HandleNetworkUIPromptControllerClientMessage);
            }
        }

        private void HandleNetworkUIPromptControllerClientMessage(NetworkReader reader)
        {
            var messageByte = reader.ReadByte();
            if (messageByte == PickupPickerController.msgSubmit)
            {
                int choiceIndex = reader.ReadInt32();
                HandleBodySelected(choiceIndex);
                networkUIPromptController.SetParticipantMaster(null);
                return;
            }
            if (messageByte == PickupPickerController.msgCancel)
            {
                networkUIPromptController.SetParticipantMaster(null);
                if (selectedBody == BodyIndex.None && purchaseCount < maxPurchaseCount) purchaseInteraction.SetAvailable(true);
            }
        }

        private void OnDisplayBegin(NetworkUIPromptController networkUIPromptController, LocalUser localUser, CameraRigController cameraRigController)
        {
            panelInstance = Instantiate(pickerPanelPrefab, cameraRigController.hud.mainContainer.transform);
            panelInstanceController = panelInstance.GetComponent<BodyPickerPanel>();
            panelInstanceController.signalReceiver = this;
            panelInstanceController.SetPickupOptions(bodyOptions);
            OnDestroyCallback.AddCallback(panelInstance, new System.Action<OnDestroyCallback>(OnPanelDestroyed));
        }

        private void OnDisplayEnd(NetworkUIPromptController networkUIPromptController, LocalUser localUser, CameraRigController cameraRigController)
        {
            Destroy(panelInstance);
            panelInstance = null;
            panelInstanceController = null;
        }

        public void SubmitBodyPickerChoice(int choiceIndex)
        {
            if (!NetworkServer.active)
            {
                NetworkWriter networkWriter = networkUIPromptController.BeginMessageToServer();
                networkWriter.Write(PickupPickerController.msgSubmit);
                networkWriter.Write(choiceIndex);
                networkUIPromptController.FinishMessageToServer(networkWriter);
                return;
            }
            else
            {
                networkUIPromptController.SetParticipantMaster(null);
            }
            HandleBodySelected(choiceIndex);
        }

        private void OnPanelDestroyed(OnDestroyCallback onDestroyCallback)
        {
            NetworkWriter networkWriter = networkUIPromptController.BeginMessageToServer();
            networkWriter.Write(PickupPickerController.msgCancel);
            networkUIPromptController.FinishMessageToServer(networkWriter);

            if (NetworkServer.active && selectedBody == BodyIndex.None && purchaseCount < maxPurchaseCount)
            {
                purchaseInteraction.SetAvailable(true);
            }
        }

        private void RebuildBodyOptions()
        {
            if (!NetworkServer.active) return;

            bodyOptions.Clear();
            if (TeleporterInteraction.instance)
            {
                var bossDirector = TeleporterInteraction.instance.bossDirector;
                if (bossDirector)
                {
                    for (var i = 0; i < bossDirector.finalMonsterCardsSelection.Count; i++)
                    {
                        var weightedSelectionChoice = bossDirector.finalMonsterCardsSelection.GetChoice(i);
                        var spawnCard = weightedSelectionChoice.value.spawnCard;
                        var body = spawnCard.prefab.GetComponent<CharacterMaster>().bodyPrefab.GetComponent<CharacterBody>();
                        var isChampion = body.isChampion;
                        var characterSpawnCard = spawnCard as CharacterSpawnCard;
                        var forbiddenAsBoss = characterSpawnCard != null && characterSpawnCard.forbiddenAsBoss;
                        if (isChampion && !forbiddenAsBoss && weightedSelectionChoice.value.IsAvailable())
                        {
                            bodyOptions.Add(body.bodyIndex);
                        }
                    }
                }
            }
            new SyncBodyOptions(networkIdentity.netId, bodyOptions).Send(NetworkDestination.Clients);
        }

        private void HandleBodySelected(int choiceIndex)
        {
            if (!NetworkServer.active) return;
            if ((ulong)choiceIndex >= (ulong)((long)bodyOptions.Count)) return;
            selectedBody = bodyOptions[choiceIndex];
            AddShrineStack(purchaseInteraction.lastActivator);
        }

        public void FixedUpdate()
        {
            if (waitingForRefresh)
            {
                refreshTimer -= Time.fixedDeltaTime;
                if (refreshTimer <= 0f && purchaseCount < maxPurchaseCount)
                {
                    purchaseInteraction.SetAvailable(true);
                    purchaseInteraction.Networkcost = (int)(100f * (1f - Mathf.Pow(1f - (float)purchaseInteraction.cost / 100f, costMultiplierPerPurchase)));
                    waitingForRefresh = false;
                }
            }

            if (bodyOptions.Count <= 0 && NetworkServer.active)
            {
                buildBodyOptionsAttemptTimer -= Time.fixedDeltaTime;
                if (buildBodyOptionsAttemptTimer <= 0)
                {
                    buildBodyOptionsAttemptTimer = 2f;
                    RebuildBodyOptions();
                }
            }
        }

        [Server]
        public void AddShrineStack(Interactor interactor)
        {
            waitingForRefresh = true;
            if (TeleporterInteraction.instance && TeleporterInteraction.instance.activationState <= TeleporterInteraction.ActivationState.IdleToCharging)
            {
                var tpComponent = TeleporterInteraction.instance.GetComponent<ExtraChallengeShrinesTeleporterComponent>();
                if (!tpComponent) return;

                tpComponent.eyeShrineStacks++;

                var bonusYellowChance = ShrineEye.yellowChance / 100f;
                if (ShrineEye.yellowChanceStackingReduction) bonusYellowChance /= (float)Mathf.Max(tpComponent.eyeShrineStacks, 1);
                tpComponent.bossGroup.bossDropChance += bonusYellowChance;

                if (selectedBody != BodyIndex.None) tpComponent.eyeSelectedBody = selectedBody;

                tpComponent.ServerSendSyncShrineStacks();
            }
            CharacterBody component = interactor.GetComponent<CharacterBody>();
            Chat.SendBroadcastChat(new Chat.SubjectFormatChatMessage
            {
                subjectAsCharacterBody = component,
                baseToken = "EXTRACHALLENGESHRINES_SHRINE_EYE_USE_MESSAGE"
            });
            EffectManager.SpawnEffect(LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/ShrineUseEffect"), new EffectData
            {
                origin = transform.position,
                rotation = Quaternion.identity,
                scale = 1f,
                color = new Color(0.7372549f, 0.905882359f, 0.945098042f)
            }, true);
            purchaseCount++;
            refreshTimer = 2f;
            if (purchaseCount >= maxPurchaseCount)
            {
                symbolTransform.gameObject.SetActive(false);
            }
        }

        public override int GetNetworkChannel()
        {
            return QosChannelIndex.defaultReliable.intVal;
        }

        public class SyncBodyOptions : INetMessage
        {
            NetworkInstanceId objID;
            List<BodyIndex> bodyOptions;

            public SyncBodyOptions()
            {
            }

            public SyncBodyOptions(NetworkInstanceId objID, List<BodyIndex> bodyOptions)
            {
                this.objID = objID;
                this.bodyOptions = bodyOptions;
            }

            public void Deserialize(NetworkReader reader)
            {
                objID = reader.ReadNetworkId();
                var bodyOptionsCount = reader.ReadInt32();
                bodyOptions = new List<BodyIndex>();
                for (var i = 0; i < bodyOptionsCount; i++)
                {
                    bodyOptions.Add((BodyIndex)reader.ReadInt32());
                }
            }

            public void OnReceived()
            {
                if (NetworkServer.active) return;
                GameObject obj = Util.FindNetworkObject(objID);
                if (obj)
                {
                    ExtraChallengeShrinesShrineEyeBehaviour component = obj.GetComponent<ExtraChallengeShrinesShrineEyeBehaviour>();
                    if (component)
                    {
                        component.bodyOptions = bodyOptions;
                    }
                }
            }

            public void Serialize(NetworkWriter writer)
            {
                writer.Write(objID);
                writer.Write(bodyOptions.Count);
                for (var i = 0; i < bodyOptions.Count; i++)
                {
                    writer.Write((int)bodyOptions[i]);
                }
            }
        }
    }

    public interface IBodyPickerPanelSignalReceiver
    {
        void SubmitBodyPickerChoice(int choiceIndex);
    }

    public class BodyPickerPanel : MonoBehaviour
    {
        public GridLayoutGroup gridlayoutGroup;
        public RectTransform buttonContainer;
        public GameObject buttonPrefab;
        public Image[] coloredImages;
        public Image[] darkColoredImages;
        public int maxColumnCount;
        private UIElementAllocator<MPButton> buttonAllocator;
        public IBodyPickerPanelSignalReceiver signalReceiver;

        public void Awake()
        {
            buttonAllocator = new UIElementAllocator<MPButton>(buttonContainer, buttonPrefab, true, false);
            buttonAllocator.onCreateElement = new UIElementAllocator<MPButton>.ElementOperationDelegate(OnCreateButton);
            gridlayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridlayoutGroup.constraintCount = maxColumnCount;
        }

        public void OnCreateButton(int index, MPButton button)
        {
            button.onClick.AddListener(delegate ()
            {
                signalReceiver.SubmitBodyPickerChoice(index);
            });
        }

        public void SetPickupOptions(List<BodyIndex> options)
        {
            buttonAllocator.AllocateElements(options.Count);
            ReadOnlyCollection<MPButton> elements = buttonAllocator.elements;
            Sprite defaultSprite = LegacyResourcesAPI.Load<Sprite>("Textures/MiscIcons/texUnlockIcon");
            if (options.Count != 0)
            {
                Color baseColor = ColorCatalog.GetColor(ColorCatalog.ColorIndex.BossItem);
                Color darkColor = ColorCatalog.GetColor(ColorCatalog.ColorIndex.BossItemDark);
                Image[] array = coloredImages;
                for (int i = 0; i < array.Length; i++)
                {
                    array[i].color *= baseColor;
                }
                array = darkColoredImages;
                for (int i = 0; i < array.Length; i++)
                {
                    array[i].color *= darkColor;
                }
            }
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            for (int j = 0; j < options.Count; j++)
            {
                MPButton mpbutton = elements[j];
                int num = j - j % maxColumnCount;
                int num2 = j % maxColumnCount;
                int num3 = num2 - maxColumnCount;
                int num4 = num2 - 1;
                int num5 = num2 + 1;
                int num6 = num2 + maxColumnCount;
                Navigation navigation = mpbutton.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                if (num4 >= 0)
                {
                    MPButton selectOnLeft = elements[num + num4];
                    navigation.selectOnLeft = selectOnLeft;
                }
                if (num5 < maxColumnCount && num + num5 < options.Count)
                {
                    MPButton selectOnRight = elements[num + num5];
                    navigation.selectOnRight = selectOnRight;
                }
                if (num + num3 >= 0)
                {
                    MPButton selectOnUp = elements[num + num3];
                    navigation.selectOnUp = selectOnUp;
                }
                if (num + num6 < options.Count)
                {
                    MPButton selectOnDown = elements[num + num6];
                    navigation.selectOnDown = selectOnDown;
                }
                mpbutton.navigation = navigation;
                var body = BodyCatalog.GetBodyPrefabBodyComponent(options[j]);
                var component = mpbutton.GetComponent<ChildLocator>().FindChild("Icon").GetComponent<Image>();
                var iconGameObject = component.gameObject;
                foreach (var image in component.GetComponents<Image>().ToList())
                {
                    DestroyImmediate(image);
                }
                var rawImage = iconGameObject.AddComponent<RawImage>();
                rawImage.color = Color.white;
                rawImage.texture = (body != null) ? body.portraitIcon : defaultSprite.texture;
                mpbutton.interactable = true;
            }
        }
    }
}
