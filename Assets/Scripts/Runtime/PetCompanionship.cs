using System;
using ElecPet.Core;
using UnityEngine;

namespace ElecPet.Runtime
{
    // 关系、生活目标与保存调度在此汇合，Controller 只负责输入和画面更新的顺序。
    public sealed class PetCompanionship
    {
        private readonly PetSaveStore store;
        private readonly Action<string, Exception> report;
        private bool dirty = true, saveFailed;
        private double saveAt = 30;
        public PetSaveData Data { get; private set; }
        public PetLife Life { get; private set; }

        public PetCompanionship(string directory, DesktopRect area, Func<PetSaveData> migrate, Action<string, Exception> report)
        {
            this.report = report;
            store = new PetSaveStore(directory, text => JsonUtility.FromJson<PetSaveData>(text), data => JsonUtility.ToJson(data, true));
            store.Report = report;
            Data = store.Load(migrate);
            Life = new PetLife(area);
        }

        public void Interrupt(PetBehavior behavior)
        {
            if (Life.State == PetLifeState.RestingNearOwner) behavior.StopRest();
            Life.Interrupt();
        }

        public void Resize(DesktopRect newArea, PetBehavior behavior)
        {
            Interrupt(behavior);
            Life.Resize(newArea);
        }

        public void Track(PetBehavior behavior)
        {
            if (Data.Familiarity == behavior.Familiarity && Data.HeadPreference == behavior.HeadPreference) return;
            Data.Familiarity = behavior.Familiarity; Data.HeadPreference = behavior.HeadPreference;
            dirty = true;
        }

        // 使用实际运行时间调度落盘，暂停或隐藏只冻结生活行为，不阻止保存已有进度。
        public bool Save(double realTime, PetBehavior behavior, bool immediate = false)
        {
            Track(behavior);
            if (!dirty || !store.CanSave || (!immediate && realTime < saveAt)) return false;
            saveAt = realTime + 30;
            try
            {
                store.Save(Data);
                dirty = saveFailed = false;
                return true;
            }
            catch (Exception exception)
            {
                if (!saveFailed && report != null) report("保存宠物进度失败，将在下一周期重试", exception);
                saveFailed = true;
                return false;
            }
        }

    }
}
