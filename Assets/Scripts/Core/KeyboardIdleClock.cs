using System;
using System.Threading;

namespace ElecPet.Core
{
    // 只保存最后一次键盘活动的时间，不读取或记录键码、字符和输入内容。
    public sealed class KeyboardIdleClock
    {
        private long lastInput;
        public KeyboardIdleClock(long timestamp) { lastInput = timestamp; }
        public void Record(uint message, long timestamp)
        {
            if (message == 0x100 || message == 0x101 || message == 0x104 || message == 0x105)
                Interlocked.Exchange(ref lastInput, timestamp);
        }
        public double Seconds(long timestamp) { return Math.Max(0, timestamp - Interlocked.Read(ref lastInput)) / 1000.0; }
    }
}
