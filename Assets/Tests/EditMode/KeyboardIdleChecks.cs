using System;
using ElecPet.Core;

namespace ElecPet.Tests
{
    public static class KeyboardIdleChecks
    {
        public static int Run()
        {
            int count = 0;
            var idle = new KeyboardIdleClock(1000);
            idle.Record(0x0200, 30000);
            idle.Record(0x0201, 30500);
            idle.Record(0x020A, 30900);
            Check(idle.Seconds(31000) == 30, "鼠标移动、点击和滚轮不重置键盘空闲", ref count);
            idle.Record(0x0100, 31000);
            Check(idle.Seconds(60999) < 30 && idle.Seconds(61000) == 30, "键盘按下重新等待30秒", ref count);
            idle.Record(0x0101, 62000);
            Check(idle.Seconds(62000) == 0, "键盘松开也重置计时", ref count);
            idle.Record(0x0104, 63000);
            idle.Record(0x0105, 64000);
            Check(idle.Seconds(94000) == 30, "系统组合键同样计入键盘活动", ref count);
            Check(idle.Seconds(63000) == 0, "计时回退不产生负空闲时间", ref count);
            return count;
        }
        private static void Check(bool ok, string label, ref int count)
        { if (!ok) throw new InvalidOperationException(label); count++; }
    }
}
