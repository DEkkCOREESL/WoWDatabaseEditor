using Avalonia.Input;
using Avalonia.Threading;
using TheEngine;
using TheEngine.Data;
using TheEngine.ECS;
using TheEngine.Entities;
using TheEngine.Interfaces;
using TheEngine.PhysicsSystem;
using TheMaths;
using IInputManager = TheEngine.Interfaces.IInputManager;
using MouseButton = TheEngine.Input.MouseButton;

namespace WDE.MapRenderer.Utils
{
    public class Gizmo : System.IDisposable
    {
        private readonly IMeshManager meshManager;
        public readonly Transform position = new();
        private readonly IMesh arrowMesh;
        private readonly IMesh dragPlaneMesh;
        private readonly Material material;
        private readonly Transform t = new Transform();
        private bool ownsMeshes;

        public Gizmo(IMeshManager meshManager, IMaterialManager materialManager)
        {
            this.meshManager = meshManager;
            arrowMesh = meshManager.CreateMesh(ObjParser.LoadObj("meshes/arrow.obj").MeshData);
            dragPlaneMesh = meshManager.CreateMesh(ObjParser.LoadObj("meshes/dragPlane.obj").MeshData);
            this.material = materialManager.CreateMaterial("data/gizmo.json");
            ownsMeshes = true;
        }

        public void Dispose()
        {
            if (ownsMeshes)
            {
                meshManager.DisposeMesh(arrowMesh);
                meshManager.DisposeMesh(dragPlaneMesh);
                ownsMeshes = false;
            }
        }

        public Gizmo(IMesh arrowMesh, IMesh dragPlaneMesh, Material material)
        {
            this.arrowMesh = arrowMesh;
            this.dragPlaneMesh = dragPlaneMesh;
            this.material = material;
        }

        private static readonly Quaternion ArrowX = Quaternion.LookRotation(Vector3.Left, Vector3.Up);
        private static readonly Quaternion ArrowY = Quaternion.LookRotation(Vector3.Backward, Vector3.Up);
        private static readonly Quaternion ArrowZ = Quaternion.FromEuler(0, 0, -90);
        
        private static readonly Quaternion PlaneZ = Quaternion.LookRotation(Vector3.Forward, Vector3.Up);
        private static readonly Quaternion PlaneY = Quaternion.FromEuler(-90, 0, -90);
        private static readonly Quaternion PlaneX = Quaternion.LookRotation(Vector3.Up, Vector3.Up);

        // wow direction hit test
        public enum HitType
        {
            None,
            TranslateX,
            TranslateZY,
            TranslateZ,
            TranslateXY,
            TranslateY,
            TranslateXZ
        }

        public HitType HitTest(IInputManager inputManager, ICameraManager cameraManager, out Vector3 intersectionPoint)
        {
            intersectionPoint = Vector3.Zero;
            t.Position = position.Position;
            
            var ray = cameraManager.MainCamera.NormalizedScreenPointToRay(inputManager.Mouse.NormalizedPosition);

            t.Rotation = PlaneX;
            if (Physics.RayIntersectsObject(dragPlaneMesh, 0,  t.LocalToWorldMatrix, ray, null, out intersectionPoint))
                return HitType.TranslateZY;
            
            t.Rotation = PlaneY;
            if (Physics.RayIntersectsObject(dragPlaneMesh, 0, t.LocalToWorldMatrix, ray, null, out intersectionPoint))
                return HitType.TranslateXZ;
            
            t.Rotation = PlaneZ;
            if (Physics.RayIntersectsObject(dragPlaneMesh, 0, t.LocalToWorldMatrix, ray, null, out intersectionPoint))
                return HitType.TranslateXY;
            
            t.Rotation = ArrowX;
            if (Physics.RayIntersectsObject(arrowMesh, 0, t.LocalToWorldMatrix, ray, null, out intersectionPoint))
                return HitType.TranslateX;
            
            t.Rotation = ArrowY;
            if (Physics.RayIntersectsObject(arrowMesh, 0, t.LocalToWorldMatrix, ray, null, out intersectionPoint))
                return HitType.TranslateY;

            t.Rotation = ArrowZ;
            if (Physics.RayIntersectsObject(arrowMesh, 0, t.LocalToWorldMatrix, ray, null, out intersectionPoint))
                return HitType.TranslateZ;

            return HitType.None;
        }

        public void Render(ICameraManager cameraManager, IRenderManager renderManager)
        {
            InternalRender(cameraManager, renderManager, true);
            InternalRender(cameraManager, renderManager, false);
        }

        private void InternalRender(ICameraManager cameraManager, IRenderManager renderManager, bool transparent)
        {
            if (transparent)
            {
                material.BlendingEnabled = true;
                material.SourceBlending = Blending.SrcAlpha;
                material.DestinationBlending = Blending.OneMinusSrcAlpha;
                material.DepthTesting = DepthCompare.Always;
                material.ZWrite = false;
            }
            else
            {
                material.BlendingEnabled = false;
                material.DepthTesting = DepthCompare.Lequal;
                material.ZWrite = true;
            }
            
            var dist = (position.Position - cameraManager.MainCamera.Transform.Position).Length();
            t.Position = position.Position;
            t.Scale = Vector3.One * (float)Math.Sqrt(Math.Clamp(dist, 0.5f, 500) / 15);
            // +X (wow)
            t.Rotation = ArrowX;
            material.SetUniform("objectColor", new Vector4(0, 0, 1, transparent ? 0.5f : 1f));
            renderManager.Render(arrowMesh, material, 0, t);
            t.Rotation = PlaneX;
            renderManager.Render(dragPlaneMesh, material, 0, t);
                

            // +Y (wow)
            t.Rotation = ArrowY;
            material.SetUniform("objectColor", new Vector4(0, 1, 0, transparent ? 0.5f : 1f));
            renderManager.Render(arrowMesh, material, 0, t);
            t.Rotation = PlaneY;
            renderManager.Render(dragPlaneMesh, material, 0, t);
                
            // +Z (wow)
            t.Rotation = ArrowZ;
            material.SetUniform("objectColor", new Vector4(1, 0, 0, transparent ? 0.5f : 1f));
            renderManager.Render(arrowMesh, material, 0, t);
            t.Rotation = PlaneZ;
            renderManager.Render(dragPlaneMesh, material, 0, t);
        }
    }
    
    internal enum DragMode
    {
        NoDragging,
        MouseDrag,
        KeyboardDrag
    }
    
    public abstract class Dragger<T>
    {
        private readonly IMeshManager meshManager;
        private readonly IMaterialManager materialManager;
        private readonly ICameraManager cameraManager;
        private readonly IRenderManager renderManager;
        private readonly RaycastSystem raycastSystem;
        private readonly IInputManager inputManager = null!;
        private Gizmo gizmo = null!;
        
        private DragMode dragging = DragMode.NoDragging;

        private Plane plane;
        private Vector3? axis;
        private readonly List<(T item, Vector3 original_position, Vector3 offset)> draggable = new();
        private Vector3 originalTouch;
        public bool IsEnabled { get; set; }
        public Vector3 GizmoPosition { get; set; }
        
        public Dragger(IMeshManager meshManager,
            IMaterialManager materialManager,
            ICameraManager cameraManager,
            IRenderManager renderManager,
            RaycastSystem raycastSystem,
            IInputManager inputManager)
        {
            this.meshManager = meshManager;
            this.materialManager = materialManager;
            this.cameraManager = cameraManager;
            this.renderManager = renderManager;
            this.raycastSystem = raycastSystem;
            this.inputManager = inputManager;
            Initialize();
        }

        public void Dispose()
        {
            gizmo.Dispose();
        }
        
        public void Initialize()
        {
            gizmo = new Gizmo(meshManager, materialManager);
        }

        public void Render()
        {
            if (!IsEnabled)
                return;
            
            gizmo.position.Position = GizmoPosition + Vector3.Up;
            gizmo.Render(cameraManager, renderManager);
        }

        public abstract Vector3 GetPosition(T item);
        protected abstract void Move(T item, Vector3 position);
        protected abstract IReadOnlyList<T>? PointsToDrag();

        public bool Update(float f)
        {
            var ray = cameraManager.MainCamera.NormalizedScreenPointToRay(inputManager.Mouse.NormalizedPosition);
            
            var stopDrag = (dragging == DragMode.MouseDrag && !inputManager.Mouse.IsMouseDown(MouseButton.Left)) ||
                           (dragging == DragMode.KeyboardDrag && inputManager.Mouse.HasJustClicked(MouseButton.Left));
            if (stopDrag)
                dragging = DragMode.NoDragging;

            if (draggable.Count > 0 && stopDrag)
            {
                FinishDrag();
                return true;
            }
            
            if (dragging != DragMode.NoDragging && inputManager.Keyboard.JustPressed(Key.Escape))
            {
                foreach (var d in draggable)
                {
                    Move(d.item, d.original_position);
                }
                dragging = DragMode.NoDragging;
            }

            if (IsEnabled && dragging != DragMode.NoDragging)
            {
                if (plane.Intersects(ref ray, out Vector3 originalTouch))
                {
                    foreach (var item in draggable)
                    {
                        var touch = originalTouch;
                        if (axis.HasValue)
                            touch = item.original_position + axis.Value * Vector3.Dot(touch - this.originalTouch, axis.Value);
                        else
                            touch += item.offset;
                        Move(item.item, touch);   
                    }
                }
            }

            if (dragging == DragMode.KeyboardDrag && inputManager.Keyboard.JustPressed(Key.G))
            {
                dragging = DragMode.NoDragging;
                List<(Entity, Vector3)> result = new();
                foreach (var dragged in draggable)
                {
                    var position = GetPosition(dragged.item);
                    raycastSystem.RaycastAll(new Ray(position.WithZ(4000), Vector3.Down), position, result);
                    if (result.Count > 0)
                    {
                        float minDistance = float.MaxValue;
                        foreach (var r in result)
                        {
                            float diff = Math.Abs(r.Item2.Z - position.Z);
                            if (diff < minDistance)
                            {
                                minDistance = diff;
                                Move(dragged.item, new Vector3(position.X, position.Y, r.Item2.Z));
                            }
                        }
                        result.Clear();
                    }
                }
                FinishDrag();
                return false;
            }
            
            if (dragging == DragMode.NoDragging && inputManager.Keyboard.JustPressed(Key.G))
            {
                StartDragging(Gizmo.HitType.TranslateXY, ref ray, DragMode.KeyboardDrag);
            }

            if (dragging == DragMode.KeyboardDrag)
            {
                Gizmo.HitType hitType = Gizmo.HitType.None;
                if (inputManager.Keyboard.JustPressed(Key.X))
                    hitType = inputManager.Keyboard.IsDown(Key.LeftShift) ? Gizmo.HitType.TranslateZY : Gizmo.HitType.TranslateX;
                else if (inputManager.Keyboard.JustPressed(Key.Y))
                    hitType = inputManager.Keyboard.IsDown(Key.LeftShift) ? Gizmo.HitType.TranslateXZ : Gizmo.HitType.TranslateY;
                else if (inputManager.Keyboard.JustPressed(Key.Z))
                    hitType = inputManager.Keyboard.IsDown(Key.LeftShift) ? Gizmo.HitType.TranslateXY : Gizmo.HitType.TranslateZ;
                
                if (hitType != Gizmo.HitType.None)
                    StartDragging(hitType, ref ray, DragMode.KeyboardDrag);
            }

            if (inputManager.Mouse.HasJustClicked(MouseButton.Left) && !stopDrag)
            {
                if (IsEnabled && dragging == DragMode.NoDragging)
                {
                    var result = gizmo.HitTest(inputManager, cameraManager, out _);
                    if (result != Gizmo.HitType.None)
                        StartDragging(result, ref ray, DragMode.MouseDrag);
                }
            }

            return IsDragging();
        }

        private void FinishDrag()
        {
            draggable.Clear();
        }

        private void GetDragAxisAndPlane(Gizmo.HitType mode, out Vector3? axis, out Plane plane)
        {
            axis = null;
            plane = default;
            switch (mode)
            {
                case Gizmo.HitType.TranslateX:
                    axis = new Vector3(1, 0, 0);
                    break;
                case Gizmo.HitType.TranslateZY:
                    axis = null;
                    plane = new Plane(gizmo.position.Position, new Vector3(1, 0, 0));
                    break;
                case Gizmo.HitType.TranslateZ:
                    axis = new Vector3(0, 0, 1);
                    break;
                case Gizmo.HitType.TranslateXY:
                    axis = null;
                    plane = new Plane(gizmo.position.Position, new Vector3(0, 0, 1));
                    break;
                case Gizmo.HitType.TranslateY:
                    axis = new Vector3(0, 1, 0);
                    break;
                case Gizmo.HitType.TranslateXZ:
                    axis = null;
                    plane = new Plane(gizmo.position.Position, new Vector3(0, 1, 0));
                    break;
            }

            if (axis.HasValue)
            {
                Vector3 planeTangent = Vector3.Cross(axis.Value, gizmo.position.Position - cameraManager.MainCamera.Transform.Position);
                Vector3 planeNormal = Vector3.Cross(axis.Value, planeTangent);
                plane = new Plane(gizmo.position.Position, planeNormal);
            }
        }

        private void StartDragging(Gizmo.HitType hitType, ref Ray ray, DragMode dragMode)
        {
            GetDragAxisAndPlane(hitType, out var axis, out var plane);
            if (plane.Intersects(ref ray, out Vector3 touchPoint))
                StartDragging(touchPoint, axis, plane, dragMode);
        }
        
        private void StartDragging(Vector3 startTouchPoint, Vector3? axis, Plane plane, DragMode dragMode)
        {
            originalTouch = startTouchPoint;
            dragging = dragMode;
            this.axis = axis;
            this.plane = plane;
            draggable.Clear();
            var toDrag = PointsToDrag();
            if (toDrag != null)
            {
                foreach (var selected in toDrag)
                {
                    var originalPosition = GetPosition(selected);
                    draggable.Add((selected, originalPosition, originalPosition - startTouchPoint));
                }   
            }
        }

        public bool IsDragging()
        {
            return dragging != DragMode.NoDragging;
        }
    }
}