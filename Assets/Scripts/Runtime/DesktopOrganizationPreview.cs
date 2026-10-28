using System;
using System.Collections.Generic;
using ElecPet.Core;
using ElecPet.Windows;

namespace ElecPet.Runtime
{
    // 合成桌面与真实桌面共用规划、STA执行器和搬运流程，不访问用户文件。
    public sealed class DesktopOrganizationPreview : IDesktopIconSession
    {
        private readonly DesktopRect area;
        private readonly List<DesktopItem> items = new List<DesktopItem>();
        public DesktopOrganizationPreview(DesktopRect area)
        {
            this.area = area;
            string[] names = { "照片.png", "笔记.pdf", "工作资料", "应用快捷方式", "其他文件.zip" };
            string[] extensions = { ".png", ".pdf", "", ".lnk", ".zip" };
            double[] x = { .28, .44, .62, .36, .72 }, y = { .28, .48, .25, .65, .55 };
            for (int i = 0; i < names.Length; i++)
                items.Add(new DesktopItem("preview-" + i, names[i], extensions[i], i == 2, i == 3,
                    new Point2(area.X + area.Width * x[i], area.Y + area.Height * y[i])));
        }
        public DesktopSnapshot Read()
        { return new DesktopSnapshot(new List<DesktopItem>(items), area, new Point2(area.X + 12, area.Y + 12), new Point2(92, 86), false, "preview"); }
        public Point2 Position(string id) { return Find(id).Position; }
        public Point2 Move(string id, Point2 point)
        {
            var item = Find(id);
            items[items.IndexOf(item)] = new DesktopItem(item.Id, item.Name, item.Extension, item.Folder, item.Shortcut, point);
            return point;
        }
        private DesktopItem Find(string id)
        {
            var item = items.Find(value => value.Id == id);
            if (item == null) throw new InvalidOperationException("演示图标已不存在。");
            return item;
        }
        public void RestoreAlignment() { }
        public void Dispose() { }
    }
}
