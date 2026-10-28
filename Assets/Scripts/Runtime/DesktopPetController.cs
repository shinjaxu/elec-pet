using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using ElecPet.Core;
using ElecPet.Windows;
using UnityEngine;

namespace ElecPet.Runtime
{
    public sealed class DesktopPetController : MonoBehaviour
    {
        private Camera petCamera;
        private PrototypePet pet;
        private PetMotion motion;
        private PetBehavior behavior;
        private PetCompanionship companionship;
        private DesktopRect workArea;
        private NativeWindow window;
        private PetDesktopOrganizer organizer;
        private PetScreenCompanion screenCompanion;
        private PetSpeechBubble speechBubble;
        private PetFileFeeding feeding = new PetFileFeeding(IntPtr.Zero);
        private PetFoodVisual foodVisual = new PetFoodVisual();
        private PetFileDropTarget fileDrop;
        private bool quitting;
        private bool wasFeedingHover;
        private Texture2D feedingBubble;
        private GUIStyle feedingBubbleStyle, feedingLabelStyle;
        private DesktopSnapshot lastIconSnapshot;
        private readonly List<Vector2> icons = new List<Vector2>();
        private bool native, paused, hidden, wasLeft, wasRight, pressing, dragging, chasing, screenClickAllowed;
        private Vector2 pressPoint, dragOffset, lastPointer;
        private Vector2 huntOrigin, huntTarget;
        private PetTouch pressPart;
        private int width, height, nextIcon;
        private float visitAt = 8, visitUntil, affection, animationTime, previewIdle;
        private long previewIdleMilliseconds;
        private bool idleReadFailed;
        private static readonly KeyCode[] previewKeys = (KeyCode[])Enum.GetValues(typeof(KeyCode));
        private float workAreaRefreshAt;
        private Vector2? iconTarget;
        private string status = "散步";
        private readonly PetMouseFetch mouseFetch = new PetMouseFetch();
        private GameObject fetchBall;
        private Material fetchBallMaterial;
        private Vector3 droppedBallPosition;
        private bool FeedingAttention { get { return feeding.Flow.Progress >= 0 || fileDrop != null && fileDrop.Hovering; } }

        private IEnumerator Start()
        {
            behavior = new PetBehavior(new System.Random());
            Application.runInBackground = true;
            Application.targetFrameRate = 30;
            QualitySettings.vSyncCount = 0;
            CreateCamera();
            var light = new GameObject("柔光").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(35, -25, 0);
            light.intensity = .85f;
            // 固定环境补光，深色皮肤的层次与离线预览一致，不依赖启动场景的天空盒。
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.65f, 0.65f, 0.7f);
            var rim = new GameObject("深色皮肤轮廓光").AddComponent<Light>();
            rim.type = LightType.Directional;
            rim.transform.rotation = Quaternion.Euler(20, 140, 0);
            rim.color = new Color(.65f, .78f, 1);
            rim.intensity = .65f;
            pet = new PrototypePet();
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, FullScreenMode.Windowed);
            yield return null;
            yield return new WaitForEndOfFrame();
            try
            {
                window = new NativeWindow();
                fileDrop = new PetFileDropTarget(window.Handle);
                native = true;
                screenCompanion = new PetScreenCompanion(window.Handle, PlayerPrefs.GetInt("Pet.ScreenCompanion.Enabled", 1) != 0);
                Debug.LogFormat("文件拖放初始化完成，运行时位数={0}，悬停门槛=3秒。", IntPtr.Size * 8);
            }
            catch (Exception exception)
            {
                Debug.LogError("桌面窗口或文件拖放初始化失败；原型将退出。 " + exception);
                Application.Quit(1);
                yield break;
            }
#else
            yield return null;
#endif
            RecalculateArea();
            feeding = new PetFileFeeding(native ? window.Handle : IntPtr.Zero);
            companionship = new PetCompanionship(Application.persistentDataPath, workArea, MigrateHabits,
                (stage, exception) => Debug.LogWarning(stage + "\n" + exception));
            behavior = new PetBehavior(new System.Random(), companionship.Data.Familiarity, companionship.Data.HeadPreference);
            companionship.Save(Time.realtimeSinceStartupAsDouble, behavior, true);
        }

        private void Update()
        {
            if (motion == null) return;
            if (width != Screen.width || height != Screen.height) RecalculateArea();
            if (native && Time.unscaledTime >= workAreaRefreshAt)
            {
                workAreaRefreshAt = Time.unscaledTime + 1;
                var currentArea = NativeWindow.ReadDesktopWorkArea();
                var previousArea = window.WorkArea;
                var currentBounds = NativeWindow.ReadDesktopBounds();
                var previousBounds = window.Bounds;
                var currentScreens = NativeWindow.ReadDesktopWorkAreas();
                bool screensChanged = currentScreens.Length != window.WorkAreas.Length;
                for (int i = 0; !screensChanged && i < currentScreens.Length; i++)
                {
                    var current = currentScreens[i]; var previous = window.WorkAreas[i];
                    screensChanged = current.X != previous.X || current.Y != previous.Y || current.Width != previous.Width || current.Height != previous.Height;
                }
                if (currentArea.X != previousArea.X || currentArea.Y != previousArea.Y || currentArea.Width != previousArea.Width || currentArea.Height != previousArea.Height
                    || currentBounds.X != previousBounds.X || currentBounds.Y != previousBounds.Y || currentBounds.Width != previousBounds.Width || currentBounds.Height != previousBounds.Height || screensChanged)
                    RecalculateArea();
            }
            if (native) HandleCommand(window.TakeCommand());
            if (companionship != null) companionship.Save(Time.realtimeSinceStartupAsDouble, behavior);
            bool left = native ? NativeWindow.ButtonDown(1) : Input.GetMouseButton(0);
            bool right = native ? NativeWindow.ButtonDown(2) : Input.GetMouseButton(1);
            Point2? leftPress = native ? window.TakeLeftPress() : null;
            Point2? leftRelease = native ? window.TakeLeftRelease() : null;
            Vector2 pointer = Pointer();
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            // 快速拖入可能先被上一帧当成按住宠物；真实文件到达后立即交还拖放流程。
            if (fileDrop != null && fileDrop.DraggingFiles && pressing)
            {
                if (dragging) behavior.PutDown();
                pressing = dragging = false;
                motion.Release();
            }
            if (hidden)
            {
                CancelMouseFetch();
                if (fileDrop != null) fileDrop.SetTarget(false, false);
                if (screenCompanion != null) screenCompanion.Tick(Time.realtimeSinceStartupAsDouble, false);
                TickFeeding(dt); TickOrganizer(dt); wasLeft = left; wasRight = right; return;
            }
            bool inputBusy = left || right || (native ? NativeWindow.KeyboardBusy() : Input.anyKey);
            if (!paused && !FeedingAttention) behavior.Tick(dt, (organizer == null || !organizer.Active)
                && !mouseFetch.Active && (companionship == null || (companionship.Life.State == PetLifeState.Free && !WaitingForOwner(pointer, inputBusy))));
            Point2 previousPosition = motion.Position;
            Physics.SyncTransforms();
            bool hit = Physics.Raycast(petCamera.ScreenPointToRay(pointer), out RaycastHit result)
                && result.transform.IsChildOf(pet.Root);
            if (native) window.SetInteractive(hit || pressing);
            if (fileDrop != null)
            {
                bool available = !paused && !pressing && !feeding.Busy && !quitting;
                fileDrop.SetTarget(hit, available);
                if (fileDrop.Hovering && !wasFeedingHover) { InterruptAutonomy(); behavior.AttendFood(); }
                wasFeedingHover = fileDrop.Hovering;
                string[] files = fileDrop.TakeFiles(out FileDropPoint dropPoint);
                // 只有真实文件拖放、连续停留满三秒并松手后，才建立会产生文件操作的喂食会话。
                if (files != null && available)
                {
                    Vector2 droppedAt = ToVector(ToLocal(new Point2(dropPoint.X, dropPoint.Y)));
                    bool droppedOnPet = Physics.Raycast(petCamera.ScreenPointToRay(droppedAt), out RaycastHit dropHit)
                        && dropHit.transform.IsChildOf(pet.Root);
                    if (droppedOnPet && feeding.BeginFiles(files)) InterruptAutonomy();
                }
            }

            // 先处理拖拽与菜单，再处理自主行为；桌面其他位置的点击不会驱动宠物。
            if (right && !wasRight && hit && native)
                HandleCommand((PetCommand)window.ContextMenu(paused, RelationshipLabel(), organizer == null ? "" : organizer.Status,
                    screenCompanion == null ? "" : screenCompanion.Status, screenCompanion != null && screenCompanion.Enabled));
            // 消费原生按下、松开位置，同一帧内完成的短点击也能保留，且不借用桌面其他位置的点击。
            if (!paused && !pressing && (left && !wasLeft || leftPress.HasValue)
                && (fileDrop == null || !fileDrop.DraggingFiles))
            {
                Vector2 pressedAt = leftPress.HasValue ? ToVector(ToLocal(leftPress.Value)) : pointer;
                if (Physics.Raycast(petCamera.ScreenPointToRay(pressedAt), out RaycastHit pressHit)
                    && pressHit.transform.IsChildOf(pet.Root)) BeginPress(pressedAt, pressHit.transform);
            }
            if (pressing && left && !leftRelease.HasValue) DragPress(pointer);
            if (pressing && (!left || leftRelease.HasValue))
                EndPress(leftRelease.HasValue ? ToVector(ToLocal(leftRelease.Value)) : pointer);
            TickMouseFetch(pointer, dt, inputBusy);
            if (!paused && !pressing && !FeedingAttention)
            {
                float pointerSpeed = Vector2.Distance(pointer, lastPointer) / Mathf.Max(dt, .001f);
                // 缓慢划过身体才算抚摸，快速掠过或桌面点击不会持续触发迎合。
                if ((!mouseFetch.Active || mouseFetch.Phase == MouseFetchPhase.Stalking)
                    && !left && !right && hit && pointerSpeed > 15 && pointerSpeed < 280)
                {
                    InterruptAutonomy();
                    behavior.Touch(TouchPart(result.transform), true);
                }
                // 整理期间不因普通鼠标靠近触发挥爪问候，明确抚摸和提起仍由前面的互动分支处理。
                if (!mouseFetch.Active && !left && !right && (organizer == null || !organizer.Active)
                    && (companionship == null || companionship.Life.State == PetLifeState.Free)
                    && Vector2.Distance(pointer, ToVector(motion.Position)) < 190)
                    behavior.NoticePointer();
            }
            TickFeeding(dt);
            if (screenCompanion != null) screenCompanion.Tick(Time.realtimeSinceStartupAsDouble,
                !paused && !dragging && !FeedingAttention && !feeding.ShowNotice && !quitting);
            TickOrganizer(dt);
            if (!paused && !pressing) UpdateAutonomy(pointer, dt, inputBusy);
            UpdatePresentation(pointer, previousPosition, dt);
            if (companionship != null) companionship.Track(behavior);
            wasLeft = left; wasRight = right; lastPointer = pointer;
            RefreshIcons();
        }

        private void TickFeeding(float dt)
        {
            if (feeding == null) return;
            bool wasBusy = feeding.Busy;
            feeding.Tick(dt, !paused && !hidden && !pressing && !quitting);
            var failure = feeding.TakeError();
            if (failure != null)
                Debug.LogWarningFormat("文件喂食未完成，异常类型={0}，错误码={1}。", failure.GetType().Name, failure.HResult);
            if (wasBusy && !feeding.Busy)
            {
                motion.Release();
                visitAt = Time.unscaledTime + 8;
                if (feeding.Flow.Succeeded > 0)
                    Debug.LogFormat("文件喂食结束，已回收={0}，未成功={1}。", feeding.Flow.Succeeded, feeding.Flow.Failed);
            }
            // 已提交的系统操作先收取真实结果，再退出，避免中途杀死回收线程。
            if (quitting && !feeding.HasPendingRecycle) Application.Quit();
        }

        private void BeginPress(Vector2 pointer, Transform target)
        {
            // 先记下手势资格，避免取消喂食后长按到提示消失，又被当成新的看图点击。
            screenClickAllowed = !paused && !hidden && !quitting && !feeding.Busy && !FeedingAttention
                && !feeding.ShowNotice && (fileDrop == null || !fileDrop.DraggingFiles);
            feeding.Cancel();
            pressing = true; pressPoint = pointer;
            dragOffset = ToVector(motion.Position) - pointer;
            pressPart = TouchPart(target);
            InterruptAutonomy();
        }

        private void DragPress(Vector2 pointer)
        {
            if (!dragging && Vector2.Distance(pointer, pressPoint) > 6)
            {
                dragging = true;
                behavior.PickUp();
            }
            if (!dragging) { status = "触碰"; return; }
            motion.DragTo(ToPoint(pointer + dragOffset)); status = "被提起";
        }

        private void EndPress(Vector2 pointer)
        {
            // 松开帧也核对位移；快速移动并松手仍属于提起，不能触发看图。
            DragPress(pointer);
            bool requestScreen = screenClickAllowed && !dragging;
            if (dragging) behavior.PutDown();
            else behavior.Touch(pressPart, false);
            pressing = dragging = false;
            screenClickAllowed = false;
            motion.Release(); visitAt = Time.unscaledTime + 8;
            if (requestScreen && screenCompanion != null && !paused && !hidden && !quitting
                && !feeding.Busy && !FeedingAttention && !feeding.ShowNotice)
                screenCompanion.RequestImmediate(Time.realtimeSinceStartupAsDouble);
        }

        private void CreateCamera()
        {
            petCamera = new GameObject("桌宠相机").AddComponent<Camera>();
            // 使用俯视的正交相机与统一地面投影，纵向移动也能对应真实的前后行走。
            petCamera.transform.position = new Vector3(0, 80, -80);
            petCamera.transform.LookAt(Vector3.zero);
            petCamera.orthographic = true;
            petCamera.clearFlags = CameraClearFlags.SolidColor;
            petCamera.backgroundColor = new Color(0, 0, 0, 0);
            petCamera.allowHDR = false;
            petCamera.allowMSAA = false;
        }

        private Vector3 WorldPosition(Point2 point)
        {
            Ray ray = petCamera.ScreenPointToRay(new Vector3((float)point.X, (float)point.Y, 0));
            new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance);
            return ray.GetPoint(distance);
        }

        private void UpdatePresentation(Vector2 pointer, Point2 previousPosition, float dt)
        {
            Vector3 position = WorldPosition(motion.Position);
            if (!paused)
            {
                animationTime += dt;
                affection = Mathf.MoveTowards(affection, behavior.State == PetState.Petted ? 1 : 0, dt * 2);
                Vector3 towardPointer = WorldPosition(ToPoint(pointer)) - position;
                Vector2 look = Vector2.ClampMagnitude(new Vector2(towardPointer.x, towardPointer.z) / 1.8f, 1);
                if (organizer != null && organizer.Active)
                {
                    Vector3 towardIcon = WorldPosition(ToLocal(organizer.LookTarget)) - position;
                    look = Vector2.ClampMagnitude(new Vector2(towardIcon.x, towardIcon.z), 1);
                }
                bool moving = Vector2.Distance(ToVector(previousPosition), ToVector(motion.Position)) > .05f;
                Vector3 travel = position - WorldPosition(previousPosition);
                // 步态接收同一地面上的真实速度，不能把屏幕 Y 像素直接当作地面 Z 距离。
                Vector2 velocity = moving && !pressing ? new Vector2(travel.x, travel.z) * (100 / dt) : Vector2.zero;
                if (mouseFetch.Phase == MouseFetchPhase.Stalking)
                    velocity = new Vector2(towardPointer.x, towardPointer.z).normalized * 65;
                if (mouseFetch.Phase == MouseFetchPhase.Spiraling)
                {
                    float angle = (float)mouseFetch.FlightAngle;
                    velocity = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle)) * 120;
                }
                if (behavior.State == PetState.Hunting)
                {
                    Vector3 huntDirection = WorldPosition(ToPoint(huntTarget)) - WorldPosition(ToPoint(huntOrigin));
                    velocity = new Vector2(huntDirection.x, huntDirection.z).normalized * 65;
                    look = velocity / 65;
                }
                PetState pose = behavior.State;
                float progress = (float)behavior.Progress;
                if (companionship != null)
                {
                    var life = companionship.Life;
                    if (life.State == PetLifeState.RestingNearOwner) { pose = PetState.Lying; progress = .5f; look = Vector2.zero; }
                }
                float feedingProgress = (float)feeding.Flow.Progress;
                if (fileDrop != null && fileDrop.Armed && !feeding.Busy) feedingProgress = .25f;
                if (FeedingAttention)
                {
                    pose = PetState.Wander; look = Vector2.zero; velocity = Vector2.zero; moving = false;
                    // 松手后再转向桌面前方，既能看清进食，也不因旋转移开悬停中的翅尾碰撞体。
                    if (feeding.Busy)
                        pet.Root.localRotation = Quaternion.RotateTowards(pet.Root.localRotation, Quaternion.Euler(0, -15, 0), dt * 240);
                }
                pet.Animate(look, animationTime, moving && !pressing, affection,
                    mouseFetch.Active ? PetState.Wander : pose, progress, behavior.TouchPart, behavior.Relationship != PetRelationship.New, velocity,
                    mouseFetch.Carrying ? 1 : organizer == null ? 0 : (float)organizer.Grip,
                    mouseFetch.Active ? (float)mouseFetch.Bend : organizer == null ? 0 : (float)organizer.Bend,
                    feedingProgress, feeding.Flow.Phase == FeedingPhase.Failed,
                    mouseFetch.Phase == MouseFetchPhase.Stalking ? 1 : 0,
                    mouseFetch.Phase == MouseFetchPhase.Spiraling ? Mathf.Sin((float)mouseFetch.FlightProgress * Mathf.PI) : 0);
            }
            if (mouseFetch.Phase == MouseFetchPhase.Spiraling)
            {
                float t = (float)mouseFetch.FlightProgress;
                float radius = Mathf.Sin(t * Mathf.PI) * .48f;
                float angle = (float)mouseFetch.FlightAngle;
                position += new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(t * Mathf.PI) * .95f,
                    Mathf.Sin(angle) * radius);
                Vector3 projected = petCamera.WorldToScreenPoint(position);
                var visible = new Point2(projected.x, projected.y);
                position += WorldPosition(motion.SafeTarget(visible)) - WorldPosition(visible);
            }
            pet.Root.position = position;
            PresentFetchBall();
            foodVisual.Present(pet, petCamera, paused ? -1 : (float)feeding.Flow.Progress,
                feeding.Extension, feeding.Flow.Phase == FeedingPhase.Failed);
        }

        // 先判定互动资格，再推进手势和安全移动；已有捡球任务独占自主动作。
        private void TickMouseFetch(Vector2 pointer, float dt, bool mouseBusy)
        {
            bool wasActive = mouseFetch.Active;
            bool available = !paused && !hidden && !pressing && !mouseBusy && !FeedingAttention && !quitting
                && (wasActive || (organizer == null || !organizer.Active)
                    && (behavior.State == PetState.Wander || behavior.IsIdleAction || behavior.State == PetState.Greeting)
                    && (companionship == null || companionship.Life.State == PetLifeState.Free));
            Point2 target = ToPoint(pointer);
            // 返回时先裁剪鼠标目标，才能在屏幕边沿满足接近条件并完成飞行。
            if (mouseFetch.Phase == MouseFetchPhase.Returning) target = motion.SafeTarget(target);
            mouseFetch.Tick(dt, motion.Position, target, available);
            mouseFetch.ConstrainTargets(motion.SafeTarget);
            if (!mouseFetch.Active)
            {
                if (wasActive) { motion.Release(); visitAt = Time.unscaledTime + 8; }
                return;
            }
            if (!wasActive)
            {
                if (companionship != null) companionship.Interrupt(behavior);
                behavior.AttendFood();
                chasing = false; iconTarget = null;
            }
            if (mouseFetch.Phase == MouseFetchPhase.Fetching || mouseFetch.Phase == MouseFetchPhase.Returning)
                motion.DragTo(DesktopGeometry.MoveTowards(motion.Position, mouseFetch.Target, dt * 420));
            status = mouseFetch.Phase == MouseFetchPhase.Stalking ? "伏低蓄势，盯着鼠标"
                : mouseFetch.Phase == MouseFetchPhase.Fetching ? "飞奔捡球"
                : mouseFetch.Phase == MouseFetchPhase.PickingUp ? "低头叼球"
                : mouseFetch.Phase == MouseFetchPhase.Returning ? "叼球追随鼠标"
                : mouseFetch.Phase == MouseFetchPhase.Spiraling ? "绕飞两圈" : "落地放球";
        }

        private void CancelMouseFetch()
        {
            bool active = mouseFetch.Active;
            mouseFetch.Cancel();
            if (fetchBall != null) fetchBall.SetActive(false);
            if (active && motion != null) motion.Release();
        }

        private void PresentFetchBall()
        {
            if (!mouseFetch.BallVisible)
            {
                if (fetchBall != null) fetchBall.SetActive(false);
                return;
            }
            if (fetchBall == null)
            {
                fetchBall = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                fetchBall.name = "鼠标互动小球";
                // 纯视觉物件不抢宠物命中与桌面鼠标穿透。
                DestroyFetchObject(fetchBall.GetComponent<Collider>());
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(fetchBall, pet.Root.gameObject.scene);
                fetchBallMaterial = new Material(Shader.Find("Standard"));
                fetchBallMaterial.color = new Color(1, .58f, .12f);
                fetchBallMaterial.SetFloat("_Glossiness", .65f);
                fetchBall.GetComponent<Renderer>().sharedMaterial = fetchBallMaterial;
            }
            fetchBall.SetActive(true);
            Vector3 ballPosition = WorldPosition(mouseFetch.Ball) + Vector3.up * .12f;
            if (mouseFetch.Carrying)
            {
                ballPosition = pet.CarryAnchor;
                droppedBallPosition = ballPosition;
            }
            else if (mouseFetch.Phase == MouseFetchPhase.Dropping)
            {
                Vector3 ground = new Vector3(droppedBallPosition.x, .12f, droppedBallPosition.z);
                droppedBallPosition = Vector3.MoveTowards(droppedBallPosition, ground, Time.unscaledDeltaTime * 2);
                ballPosition = droppedBallPosition;
            }
            fetchBall.transform.position = ballPosition;
            fetchBall.transform.localScale = Vector3.one * (.24f * (float)mouseFetch.BallOpacity);
        }

        private static void DestroyFetchObject(UnityEngine.Object value)
        {
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        private void UpdateAutonomy(Vector2 pointer, float dt, bool mouseBusy)
        {
            if (FeedingAttention || quitting) { status = feeding.Busy ? feeding.Notice : "闻到了文件，等你松手投喂"; return; }
            if (mouseFetch.Active) return;
            if (organizer != null && organizer.Active)
            {
                motion.DragTo(ToLocal(organizer.PetPosition));
                status = organizer.Status;
                return;
            }
            if (companionship != null)
            {
                var life = companionship.Life;
                bool occupied = life.State != PetLifeState.Free;
                life.Tick(dt, behavior, motion.Position, ToPoint(pointer), Vector2.Distance(pointer, lastPointer) <= 1.5f, mouseBusy);
                if (life.State != PetLifeState.Free)
                {
                    chasing = false; iconTarget = null;
                    if (life.State == PetLifeState.ApproachingOwner)
                        motion.DragTo(DesktopGeometry.MoveTowards(motion.Position, life.Target, dt * 65));
                    status = LifeStatus(life.State);
                    return;
                }
                if (occupied) motion.Release();
            }
            // 互动和自主片段拥有完整播放时间，避免被追逐或图标访问抢走动作。
            if (behavior.State != PetState.Wander)
            {
                if (behavior.State != PetState.Hunting && (chasing || iconTarget.HasValue)) InterruptAutonomy();
                if (behavior.State == PetState.Startled)
                {
                    Vector2 away = ToVector(motion.Position) - pointer;
                    if (away.sqrMagnitude < 1) away = Vector2.right;
                    motion.DragTo(ToPoint(ToVector(motion.Position) + away.normalized * dt * 45));
                    motion.Release();
                }
                if (behavior.State == PetState.Hunting)
                {
                    // 锁定起扑时的近距离目标，后续鼠标移动不改变扑跃轨迹，也不操作桌面内容。
                    float travel = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.35f, .78f, (float)behavior.Progress));
                    motion.DragTo(ToPoint(Vector2.Lerp(huntOrigin, huntTarget, travel)));
                    motion.Release();
                }
                status = BehaviorStatus();
                return;
            }
            float now = Time.unscaledTime;
            float distance = Vector2.Distance(pointer, ToVector(motion.Position));
            // 为亲近留出完整的安静观察期；否则原有扑食与巡游会先移出触发范围。
            if (WaitingForOwner(pointer, mouseBusy))
            {
                if (chasing || iconTarget.HasValue) motion.Release();
                chasing = false; iconTarget = null;
                status = "留意你，安静地等一会儿";
                return;
            }
            // 到达停止距离后仍保留跟随状态，避免归边把距离拉大后又立刻追逐。
            bool pursue = !mouseBusy && distance < 190 && !iconTarget.HasValue && (distance > 90 || chasing);
            if (pursue)
            {
                if (distance > 90 && behavior.TryHunt(distance))
                {
                    huntOrigin = ToVector(motion.Position);
                    huntTarget = huntOrigin + (pointer - huntOrigin).normalized * Mathf.Min(65, distance - 65);
                    InterruptAutonomy();
                    chasing = true;
                    status = "伏低蓄势，扑向鼠标玩耍";
                    return;
                }
                chasing = true;
                // 限制本帧位移，不能越过九十像素的停止距离；扑食落地更近时原地停稳。
                float step = Mathf.Min(dt * 65, Mathf.Max(0, distance - 90));
                motion.DragTo(DesktopGeometry.MoveTowards(motion.Position, ToPoint(pointer), step));
                status = step > .01f ? "追逐鼠标" : "停在鼠标旁";
                visitAt = now + 8;
                return;
            }
            if (chasing) { chasing = false; motion.Release(); }
            if (!iconTarget.HasValue && icons.Count > 0 && now >= visitAt)
            {
                iconTarget = icons[nextIcon++ % icons.Count] + Vector2.right * 80;
                visitUntil = now + 7;
            }
            if (iconTarget.HasValue)
            {
                status = "观察图标";
                motion.DragTo(DesktopGeometry.MoveTowards(motion.Position, ToPoint(iconTarget.Value), dt * 90));
                if (now >= visitUntil)
                {
                    iconTarget = null;
                    motion.Release();
                    visitAt = now + 12;
                }
                return;
            }
            status = affection > 0.4f ? "被抚摸" : "沿屏幕散步";
            motion.Tick(dt, false);
        }

        private void InterruptAutonomy()
        {
            CancelMouseFetch();
            if (organizer != null) organizer.Interrupt();
            if (companionship != null) companionship.Interrupt(behavior);
            chasing = false;
            iconTarget = null;
            motion.Release();
            visitAt = Time.unscaledTime + 8;
        }

        private bool WaitingForOwner(Vector2 pointer, bool mouseBusy)
        {
            if (companionship == null || mouseBusy || behavior.Energy < .35 || behavior.Relationship != PetRelationship.Trusted
                || companionship.Life.State != PetLifeState.Free || !companionship.Life.CanApproachOwner) return false;
            float distance = Vector2.Distance(pointer, ToVector(motion.Position));
            return distance >= 120 && distance <= 260;
        }

        private static PetTouch TouchPart(Transform part)
        {
            for (Transform current = part; current != null; current = current.parent)
            {
                if (current.name == "长尾") return PetTouch.Tail;
                if (current.name == "小翅膀") return PetTouch.Wing;
                if (current.name == "注视点") return PetTouch.Head;
            }
            return PetTouch.Body;
        }

        private string BehaviorStatus()
        {
            switch (behavior.State)
            {
                case PetState.Petted: return "舒服地迎合抚摸";
                case PetState.Guarding: return "回头护住翅膀或尾巴";
                case PetState.Startled: return "被戳怕了，躲一会儿";
                case PetState.Recovering: return "站稳后整理姿势";
                case PetState.Greeting: return "挥挥爪，和你打招呼";
                case PetState.Curious: return "歪头观察，伸爪试探";
                case PetState.Stretching: return "伸个懒腰";
                case PetState.Grooming: return "整理小翅膀";
                case PetState.Drowsy: return "伏卧睡觉";
                case PetState.Lying: return "卧躺休息";
                case PetState.Alert: return "抬头警戒";
                case PetState.Hunting: return "伏低蓄势，扑向鼠标玩耍";
                default: return "沿屏幕散步";
            }
        }

        private void RecalculateArea()
        {
            CancelMouseFetch();
            if (feeding != null) feeding.Cancel();
            if (fileDrop != null) fileDrop.SetTarget(false, false);
            width = Mathf.Max(1, Screen.width); height = Mathf.Max(1, Screen.height);
            petCamera.orthographicSize = height / 200f;
            var area = new DesktopRect(0, 0, width, height);
            DesktopRect[] screenAreas = null;
            if (native)
            {
                window.RefreshBounds();
                var work = window.WorkArea;
                var bottomLeft = DesktopGeometry.ScreenToLocal(new Point2(work.X, work.Y + work.Height), window.Bounds, width, height);
                var topRight = DesktopGeometry.ScreenToLocal(new Point2(work.X + work.Width, work.Y), window.Bounds, width, height);
                area = new DesktopRect(bottomLeft.X, bottomLeft.Y, topRight.X - bottomLeft.X, topRight.Y - bottomLeft.Y);
                screenAreas = new DesktopRect[window.WorkAreas.Length];
                // 每块屏幕单独转换，不能将错位屏幕之间的空隙当成可活动桌面。
                for (int i = 0; i < screenAreas.Length; i++)
                {
                    var screen = window.WorkAreas[i];
                    var low = ToLocal(new Point2(screen.X, screen.Y + screen.Height));
                    var high = ToLocal(new Point2(screen.X + screen.Width, screen.Y));
                    screenAreas[i] = new DesktopRect(low.X, low.Y, high.X - low.X, high.Y - low.Y);
                }
            }
            if (motion == null) motion = new PetMotion(area, 88, screenAreas);
            else motion.Resize(area, screenAreas);
            workArea = area;
            if (companionship != null) companionship.Resize(area, behavior);
            // 工作区变化后取消剩余扑跃位移，在当前安全位置完成动作，避免恢复旧尺寸时跳回旧轨迹。
            if (behavior.State == PetState.Hunting)
                huntOrigin = huntTarget = ToVector(motion.Position);
            icons.Clear(); iconTarget = null; lastIconSnapshot = null;
            if (organizer != null) organizer.Dispose();
            // 图标整理仍使用 Shell 的主屏工作区，避免扩大宠物活动范围时改变已有图标整理规则。
            var desktopArea = native ? NativeWindow.ReadWorkArea() : new DesktopRect(0, 0, width, height);
            Func<IDesktopIconSession> open = () => native ? (IDesktopIconSession)DesktopIcons.OpenSession() : new DesktopOrganizationPreview(desktopArea);
            Func<double> idle = native ? (Func<double>)(() => window.KeyboardIdleSeconds) : () => Interlocked.Read(ref previewIdleMilliseconds) / 1000.0;
            organizer = new PetDesktopOrganizer(new DesktopIconWorker(open, idle), desktopArea);
        }

        private void TickOrganizer(float dt)
        {
            if (organizer == null) return;
            double idleSeconds = 0;
            if (native)
            {
                try { idleSeconds = window.KeyboardIdleSeconds; idleReadFailed = false; }
                catch (Exception exception)
                {
                    if (!idleReadFailed) Debug.LogWarning("读取键盘空闲状态失败，本次停止自动整理。\n" + exception);
                    idleReadFailed = true;
                }
            }
            else
            {
                previewIdle = PreviewKeyboardActive() ? 0 : previewIdle + dt;
                idleSeconds = previewIdle;
                Interlocked.Exchange(ref previewIdleMilliseconds, (long)(previewIdle * 1000));
            }
            bool wasActive = organizer.Active;
            int completedBefore = organizer.CompletedBatches;
            Vector3 mouth = petCamera.WorldToScreenPoint(pet.CarryAnchor);
            Vector3 root = petCamera.WorldToScreenPoint(pet.Root.position);
            double sx = native ? window.Bounds.Width / width : 1, sy = native ? window.Bounds.Height / height : 1;
            double halfIcon = organizer.Latest == null ? 16 : organizer.Latest.IconSize / 2.0;
            organizer.SetGripOffset(new Point2((mouth.x - root.x) * sx - halfIcon, (root.y - mouth.y) * sy - halfIcon));
            bool available = !mouseFetch.Active && !paused && !hidden && !pressing && !feeding.Busy && !FeedingAttention && !quitting && behavior.State == PetState.Wander
                && (organizer.Active || companionship == null || companionship.Life.State == PetLifeState.Free);
            organizer.Tick(dt, idleSeconds, available, ToDesktop(motion.Position));
            if (organizer.CompletedBatches > completedBefore)
                Debug.LogFormat("桌面图标整理完成，搬运次数={0}。", organizer.CompletedMoves);
            if (!wasActive && organizer.Active)
            {
                if (companionship != null) companionship.Interrupt(behavior);
                chasing = false; iconTarget = null; motion.Release();
            }
            if (wasActive && !organizer.Active) motion.Release();
            var failure = organizer.TakeError();
            if (failure is InvalidOperationException) Debug.LogWarning("自动整理暂时停止。\n" + failure);
            else if (failure != null) Debug.LogError("桌面整理接口失败。\n" + failure);
        }

        private void RefreshIcons()
        {
            if (organizer == null || organizer.Latest == null || ReferenceEquals(lastIconSnapshot, organizer.Latest)) return;
            lastIconSnapshot = organizer.Latest; icons.Clear();
            if (iconTarget.HasValue && !organizer.Active) InterruptAutonomy();
            foreach (var item in lastIconSnapshot.Items)
            {
                var local = ToLocal(item.Position);
                if (local.X >= 0 && local.X < width && local.Y >= 0 && local.Y < height) icons.Add(ToVector(local));
            }
        }

        private static bool PreviewKeyboardActive()
        {
            if (!Input.anyKey) return false;
            // Unity 的 anyKey 包含鼠标和手柄，预览计时只接受键盘键值。
            foreach (var key in previewKeys)
                if (key > KeyCode.None && key < KeyCode.Mouse0 && Input.GetKey(key)) return true;
            return false;
        }

        private Point2 ToLocal(Point2 desktop)
        { return DesktopGeometry.ScreenToLocal(desktop, native ? window.Bounds : new DesktopRect(0, 0, width, height), width, height); }
        private Point2 ToDesktop(Point2 local)
        {
            var bounds = native ? window.Bounds : new DesktopRect(0, 0, width, height);
            return new Point2(bounds.X + local.X * bounds.Width / width, bounds.Y + (height - local.Y) * bounds.Height / height);
        }

        private Vector2 Pointer()
        {
            if (!native) return Input.mousePosition;
            return ToVector(DesktopGeometry.ScreenToLocal(NativeWindow.Cursor(), window.Bounds, width, height));
        }

        private void HandleCommand(PetCommand command)
        {
            if (command != PetCommand.None && screenCompanion != null) screenCompanion.Suspend(Time.realtimeSinceStartupAsDouble);
            if (command == PetCommand.ToggleScreenCompanion && screenCompanion != null)
            {
                screenCompanion.SetEnabled(!screenCompanion.Enabled, Time.realtimeSinceStartupAsDouble);
                PlayerPrefs.SetInt("Pet.ScreenCompanion.Enabled", screenCompanion.Enabled ? 1 : 0);
                PlayerPrefs.Save();
                Debug.LogFormat("屏幕陪伴开关已更新，启用={0}。", screenCompanion.Enabled);
            }
            if (command == PetCommand.Pause)
            {
                CancelMouseFetch();
                feeding.Cancel();
                paused = !paused;
                if (pressing)
                {
                    if (dragging) behavior.PutDown();
                    pressing = dragging = false;
                    motion.Release();
                }
                if (!paused) { visitAt = Time.unscaledTime + 8; iconTarget = null; motion.Release(); }
            }
            if (command == PetCommand.Hide)
            {
                CancelMouseFetch();
                feeding.Cancel();
                if (dragging) behavior.PutDown();
                hidden = !hidden; pressing = dragging = false; motion.Release();
                window.SetVisible(!hidden);
            }
            if (command == PetCommand.Quit)
            {
                feeding.Cancel();
                quitting = true;
                InterruptAutonomy();
                if (!feeding.HasPendingRecycle) Application.Quit();
            }
        }

        private void OnGUI()
        {
            bool hover = fileDrop != null && fileDrop.Hovering;
            if (native && !hidden && feeding != null && (feeding.ShowNotice || hover))
            {
                PrepareFeedingBubble();
                Vector3 anchor = petCamera.WorldToScreenPoint(pet.CarryAnchor);
                float x = Mathf.Clamp(anchor.x - 130, 8, Mathf.Max(8, width - 268));
                float y = Mathf.Clamp(height - anchor.y - 95, 8, Mathf.Max(8, height - 72));
                GUI.Box(new Rect(x, y, 260, 58), GUIContent.none, feedingBubbleStyle);
                string message = hover ? (fileDrop.Armed ? "准备好啦 · 松手投喂" : "停留片刻，让我闻闻…") : feeding.Notice;
                GUI.Label(new Rect(x + 10, y + 5, 240, hover ? 32 : 48), message, feedingLabelStyle);
                if (hover)
                {
                    var previousColor = GUI.color;
                    GUI.color = new Color(.27f, .33f, .36f);
                    GUI.DrawTexture(new Rect(x + 20, y + 42, 220, 3), Texture2D.whiteTexture);
                    GUI.color = fileDrop.Armed ? new Color(.65f, .89f, .55f) : new Color(.96f, .77f, .42f);
                    GUI.DrawTexture(new Rect(x + 20, y + 42, 220 * Mathf.Clamp01((float)fileDrop.HoverSeconds / 3), 3), Texture2D.whiteTexture);
                    GUI.color = previousColor;
                }
            }
            else if (native && !hidden && !paused && screenCompanion != null && !string.IsNullOrEmpty(screenCompanion.Reply))
            {
                if (speechBubble == null) speechBubble = new PetSpeechBubble();
                Vector3 anchor = petCamera.WorldToScreenPoint(pet.CarryAnchor);
                speechBubble.Draw(screenCompanion.Reply, new Vector2(anchor.x, height - anchor.y), new Vector2(width, height),
                    Time.realtimeSinceStartupAsDouble, window.DpiScale, screenCompanion.ReplyExpiresAt);
            }
            if (native || motion == null) return;
            if (organizer != null && organizer.Latest != null)
                foreach (var item in organizer.Latest.Items)
                {
                    float x = (float)item.Position.X, y = (float)item.Position.Y;
                    GUI.Box(new Rect(x, y, 32, 32), item.Folder ? "夹" : item.Shortcut ? "↗" : "▤");
                    GUI.Label(new Rect(x - 4, y + 32, 88, 22), item.Name);
                }
            float panel = Mathf.Max(12, width - 372);
            GUI.Box(new Rect(panel, 12, 360, 202), "桌面宠物 · 合成桌面演示");
            GUI.Label(new Rect(panel + 12, 42, 338, 24), "关系：" + RelationshipLabel() + " · " + (paused ? "暂停" : status));
            GUI.Label(new Rect(panel + 12, 68, 338, 44), "键盘空闲30秒后自动搬运，鼠标不计入。\n编辑器只移动演示图标，不访问真实桌面。");
            GUI.Label(new Rect(panel + 12, 116, 338, 44), organizer == null ? "" : organizer.Status + "\n键盘空闲：" + Mathf.FloorToInt(previewIdle) + " 秒");
            if (GUI.Button(new Rect(panel + 12, 168, 110, 30), paused ? "继续" : "暂停")) HandleCommand(PetCommand.Pause);
        }

        private void PrepareFeedingBubble()
        {
            if (feedingBubble != null) return;
            feedingBubble = new Texture2D(32, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[32 * 32];
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float dx = Mathf.Max(7.5f - x, x - 23.5f, 0), dy = Mathf.Max(7.5f - y, y - 23.5f, 0);
                float edge = Mathf.Sqrt(dx * dx + dy * dy);
                Color color = Color.Lerp(new Color(.055f, .075f, .095f, .94f), new Color(.16f, .21f, .24f, .96f), y / 31f);
                if (edge > 6.5f) color = Color.Lerp(color, new Color(.54f, .62f, .60f, .65f), Mathf.Clamp01(edge - 6.5f));
                color.a *= Mathf.Clamp01(8 - edge);
                pixels[y * 32 + x] = color;
            }
            feedingBubble.SetPixels(pixels); feedingBubble.Apply();
            feedingBubbleStyle = new GUIStyle { border = new RectOffset(9, 9, 9, 9) };
            feedingBubbleStyle.normal.background = feedingBubble;
            feedingLabelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 15, wordWrap = true, richText = false };
            feedingLabelStyle.normal.textColor = new Color(.94f, .96f, .93f);
        }

        private void OnDestroy()
        {
            if (fetchBall != null) DestroyFetchObject(fetchBall);
            if (fetchBallMaterial != null) DestroyFetchObject(fetchBallMaterial);
            if (feeding != null) feeding.Cancel();
            if (screenCompanion != null) screenCompanion.Dispose();
            if (speechBubble != null) speechBubble.Dispose();
            if (foodVisual != null) foodVisual.Dispose();
            if (fileDrop != null) fileDrop.Dispose();
            if (feedingBubble != null) Destroy(feedingBubble);
            if (organizer != null) organizer.Dispose();
            if (window != null) window.Dispose();
            SaveOnExit();
        }

        private void OnApplicationQuit() { if (screenCompanion != null) screenCompanion.Dispose(); if (organizer != null) organizer.Dispose(); SaveOnExit(); }
        private void SaveOnExit()
        { if (behavior != null && companionship != null) companionship.Save(Time.realtimeSinceStartupAsDouble, behavior, true); }

        private PetSaveData MigrateHabits()
        {
            var data = PetSaveData.Default();
            data.Familiarity = Mathf.Clamp01(PlayerPrefs.GetFloat("Pet.Familiarity"));
            data.HeadPreference = Mathf.Clamp01(PlayerPrefs.GetFloat("Pet.HeadPreference"));
            return data;
        }

        private string RelationshipLabel()
        { return behavior.Relationship == PetRelationship.Trusted ? "信任" : behavior.Relationship == PetRelationship.Familiar ? "熟悉" : "初识"; }

        private static string LifeStatus(PetLifeState state)
        {
            return state == PetLifeState.ApproachingOwner ? "悄悄靠近你" : "在你旁边趴一会儿";
        }
        private static Point2 ToPoint(Vector2 point) { return new Point2(point.x, point.y); }
        private static Vector2 ToVector(Point2 point) { return new Vector2((float)point.X, (float)point.Y); }
    }
}
