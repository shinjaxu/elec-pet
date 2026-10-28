using System;
using ElecPet.Core;

namespace ElecPet.Tests
{
    public static class ScreenCompanionChecks
    {
        public static int Run()
        {
            int count = 0;
            long generation;
            var clock = new ScreenObservationClock();
            clock.Observe("窗口A/标题A", 0, true);
            clock.Observe("窗口A/标题A", 15, true);
            Check(!clock.TryBegin(29.999, out generation), "停留不足30秒不触发", ref count);
            Check(clock.TryBegin(30, out generation), "连续停留30秒触发且逐帧观察不重置", ref count);
            Check(clock.IsCurrent(generation), "触发时返回当前会话代次", ref count);
            Check(!clock.TryBegin(300, out generation), "同一连续会话在冷却后仍只触发一次", ref count);

            clock = new ScreenObservationClock();
            clock.Observe("窗口A", 0, true);
            clock.Observe("窗口B", 20, true);
            Check(!clock.TryBegin(49.999, out generation), "切换窗口重新等待30秒", ref count);
            Check(clock.TryBegin(50, out generation), "切换后连续30秒可以触发", ref count);

            clock = new ScreenObservationClock();
            clock.Observe("窗口A", 0, true);
            Check(clock.TryBegin(40, out generation), "首次观察可以晚于30秒开始", ref count);
            clock.Observe("窗口B", 50, true);
            Check(!clock.TryBegin(219.999, out generation), "新会话遵守从请求发起计的180秒全局冷却", ref count);
            Check(clock.TryBegin(220, out generation), "全局冷却满180秒允许新会话触发", ref count);

            clock = new ScreenObservationClock();
            clock.Observe("窗口A", 0, true);
            clock.TryBegin(30, out generation);
            long previous = generation;
            clock.Observe("窗口B", 31, true);
            Check(!clock.IsCurrent(previous), "切换立即丢弃旧结果", ref count);
            clock.Observe("窗口A", 32, true);
            Check(!clock.IsCurrent(previous), "切回相同窗口不会复活旧结果", ref count);
            Check(clock.TryBegin(210, out generation) && clock.IsCurrent(generation), "切回的窗口使用新会话代次", ref count);

            clock = new ScreenObservationClock();
            clock.Observe("窗口A", 0, true);
            clock.TryBegin(30, out generation);
            previous = generation;
            clock.Observe("窗口A", 31, false);
            clock.Observe("窗口A", 100, false);
            Check(!clock.IsCurrent(previous) && !clock.TryBegin(200, out generation), "暂停或隐藏期间失效且不触发", ref count);
            clock.Observe("窗口A", 200, true);
            Check(!clock.TryBegin(229.999, out generation), "恢复相同窗口后重新等待30秒", ref count);
            Check(clock.TryBegin(230, out generation) && !clock.IsCurrent(previous), "恢复30秒后产生新会话", ref count);

            clock = new ScreenObservationClock();
            clock.Observe("窗口A", 0, true);
            clock.Observe(null, 29, true);
            Check(!clock.TryBegin(100, out generation), "空窗口身份使观察失效", ref count);
            clock.Observe("窗口A", 100, true);
            clock.Observe("", 129, true);
            clock.Observe("窗口A", 130, true);
            Check(!clock.TryBegin(159.999, out generation) && clock.TryBegin(160, out generation), "空字符串身份同样重置停留", ref count);

            clock = new ScreenObservationClock();
            clock.Observe("窗口A", 100, true);
            Check(!clock.TryBegin(90, out generation), "时钟回退不会触发负停留", ref count);
            Check(!clock.TryBegin(129.999, out generation) && clock.TryBegin(130, out generation), "回退后仍须达到完整停留时间", ref count);

            clock = new ScreenObservationClock();
            Check(!clock.TryBeginImmediately(0, out generation) && generation == 0, "尚无观察窗口时拒绝手动请求", ref count);
            clock.Observe(null, 0, true);
            Check(!clock.TryBeginImmediately(0, out generation), "空窗口身份不能手动请求", ref count);
            clock.Observe("", 0, true);
            Check(!clock.TryBeginImmediately(0, out generation), "空字符串窗口身份不能手动请求", ref count);
            clock.Observe("窗口A", 0, false);
            Check(!clock.TryBeginImmediately(0, out generation), "不可用观察不能手动请求", ref count);

            clock = new ScreenObservationClock();
            clock.Observe("窗口A", 0, true);
            Check(clock.TryBeginImmediately(1, out generation), "手动请求无需等待首次停留30秒", ref count);
            Check(clock.IsCurrent(generation), "手动请求返回有效代次", ref count);
            previous = generation;
            Check(!clock.TryBeginImmediately(2.999, out generation) && generation == 0, "同窗口手动请求不足2秒时拒绝", ref count);
            Check(clock.IsCurrent(previous), "拒绝手动请求不会失效已有结果", ref count);
            Check(clock.TryBeginImmediately(3, out generation), "同一会话满2秒可再次手动请求", ref count);
            Check(generation != previous && clock.IsCurrent(generation) && !clock.IsCurrent(previous), "新手动请求使同窗口旧结果立即失效", ref count);
            Check(!clock.TryBegin(30, out generation), "手动请求后停留满30秒不会自动重复", ref count);
            Check(!clock.TryBegin(300, out generation), "手动请求后同一会话冷却结束仍不自动重复", ref count);

            clock = new ScreenObservationClock();
            clock.Observe("窗口A", 0, true);
            clock.TryBeginImmediately(3, out generation);
            clock.Observe("窗口B", 3.5, true);
            Check(!clock.TryBeginImmediately(4.999, out generation), "切换窗口不能绕过手动请求2秒防抖", ref count);
            Check(clock.TryBeginImmediately(5, out generation), "切换窗口后满2秒可手动请求", ref count);
            clock.Observe("窗口B", 5.2, false);
            Check(!clock.TryBeginImmediately(5.3, out generation), "暂停后没有有效观察时拒绝手动请求", ref count);
            clock.Observe("窗口B", 5.5, true);
            Check(!clock.TryBeginImmediately(6.999, out generation), "暂停恢复不能绕过手动请求2秒防抖", ref count);
            Check(clock.TryBeginImmediately(7, out generation), "暂停恢复满2秒后可手动请求", ref count);
            clock.Observe("窗口C", 8, true);
            Check(!clock.TryBegin(186.999, out generation), "下一窗口自动请求遵守最近手动请求的180秒冷却", ref count);
            Check(clock.TryBegin(187, out generation), "最近手动请求满180秒后下一窗口可自动请求", ref count);

            clock = new ScreenObservationClock();
            clock.Observe("窗口A", 0, true);
            Check(clock.TryBegin(30, out generation), "自动请求先建立已处理会话与冷却", ref count);
            previous = generation;
            Check(clock.TryBeginImmediately(31, out generation), "手动请求可跳过自动请求后的冷却与已处理状态", ref count);
            Check(!clock.IsCurrent(previous) && clock.IsCurrent(generation), "手动请求使此前自动请求的结果失效", ref count);
            clock.Observe("窗口B", 32, true);
            Check(!clock.TryBeginImmediately(32.999, out generation), "新窗口防抖拒绝不应改变自动处理状态或冷却", ref count);
            Check(!clock.TryBegin(210.999, out generation) && clock.TryBegin(211, out generation), "拒绝手动请求不延长冷却且不阻止该会话自动请求", ref count);
            return count;
        }

        private static void Check(bool ok, string label, ref int count)
        { if (!ok) throw new InvalidOperationException(label); count++; }
    }
}
