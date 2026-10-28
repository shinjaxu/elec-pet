namespace ElecPet.Core
{
    public sealed class PetMotion
    {
        private DesktopRect bounds;
        private DesktopRect[] safeAreas;
        private readonly double margin;
        private double distance;
        private bool returning;
        public Point2 Position { get; private set; }

        public PetMotion(DesktopRect bounds, double margin, DesktopRect[] workAreas = null)
        {
            this.bounds = bounds;
            this.margin = margin;
            safeAreas = DesktopGeometry.SafeMovementAreas(workAreas ?? new[] { bounds }, margin);
            Position = DesktopGeometry.ClampToAreas(DesktopGeometry.OnPerimeter(0, bounds, margin), safeAreas);
        }

        public void Tick(double seconds, bool paused)
        {
            if (paused || seconds <= 0) return;
            if (returning)
            {
                var target = DesktopGeometry.ClampToAreas(DesktopGeometry.OnPerimeter(distance, bounds, margin), safeAreas);
                Position = DesktopGeometry.ClampToAreas(DesktopGeometry.MoveTowards(Position, target, seconds * 80), safeAreas);
                returning = Position.X != target.X || Position.Y != target.Y;
                return;
            }
            distance += seconds * 80;
            Position = DesktopGeometry.ClampToAreas(DesktopGeometry.OnPerimeter(distance, bounds, margin), safeAreas);
        }

        public void DragTo(Point2 position)
        {
            // 拖拽、追逐和扑跃共用真实工作区约束，禁止落入多屏外接矩形中的空隙。
            Position = DesktopGeometry.ClampToAreas(position, safeAreas);
        }

        public Point2 SafeTarget(Point2 position)
        {
            return DesktopGeometry.ClampToAreas(position, safeAreas);
        }

        // 松手与结束图标观察共用归边规则，避免直接跳回巡游轨道。
        public void Release()
        {
            distance = DesktopGeometry.NearestPerimeterDistance(Position, bounds, margin);
            returning = true;
        }

        public void Resize(DesktopRect newBounds, DesktopRect[] workAreas = null)
        {
            bounds = newBounds;
            safeAreas = DesktopGeometry.SafeMovementAreas(workAreas ?? new[] { bounds }, margin);
            Position = DesktopGeometry.ClampToAreas(Position, safeAreas);
            Release();
        }
    }
}
