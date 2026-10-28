using System;
using System.Globalization;
using System.IO;
using ElecPet.Core;

namespace ElecPet.Tests
{
    public static class SaveChecks
    {
        public static int Run()
        {
            int count = 0, migrations = 0;
            string directory = Path.Combine(Path.GetTempPath(), "ElecPetChecks-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = Store(directory);
                var data = store.Load(() => { migrations++; return Data(.35); });
                store.Save(data);
                Check(migrations == 1, "首次读取迁移一次", ref count);
                store = Store(directory);
                data = store.Load(() => { migrations++; return Data(0); });
                Check(data.Familiarity == .35 && migrations == 1, "重启从文件恢复关系", ref count);
                store.Save(Data(.5));
                File.WriteAllText(Path.Combine(directory, "pet.json"), "broken");
                store = Store(directory);
                data = store.Load(() => Data(0));
                Check(data.Familiarity == .35, "主文件损坏恢复备份", ref count);
                store.Save(Data(.6));
                Check(Decode(File.ReadAllText(Path.Combine(directory, "pet.backup.json"))).Familiarity == .35, "恢复后保存不覆盖有效备份", ref count);
                File.WriteAllText(Path.Combine(directory, "pet.json"), "broken-main");
                File.WriteAllText(Path.Combine(directory, "pet.backup.json"), "broken-backup");
                store = Store(directory);
                data = store.Load(() => { migrations++; return Data(.9); });
                Check(data.Familiarity == 0 && migrations == 1, "损坏不重新迁移旧进度", ref count);
                Check(Directory.GetFiles(directory, "*.corrupt-*").Length >= 2, "双份损坏保留原件", ref count);
                store.Save(data);
                var newer = Data(.7); newer.Version = 2;
                string newerText = Encode(newer);
                File.WriteAllText(Path.Combine(directory, "pet.json"), newerText);
                store = Store(directory); store.Load(() => Data(0));
                Check(!store.CanSave, "未来版本禁写", ref count);
                bool rejected = false;
                try { store.Save(Data(0)); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected && File.ReadAllText(Path.Combine(directory, "pet.json")) == newerText, "未来版本文件保留", ref count);
                foreach (double bad in new[] { double.NaN, double.PositiveInfinity, -.1, 1.1 })
                {
                    var invalid = Data(.1); invalid.Familiarity = bad;
                    Check(!invalid.IsValid(), "非法关系数值不接受", ref count);
                }
                var noVersion = Data(0); noVersion.Version = 0;
                Check(!noVersion.IsValid(), "缺少版本不接受", ref count);

                string retry = Path.Combine(directory, "retry");
                store = Store(retry); store.Load(() => Data(0));
                Directory.CreateDirectory(retry);
                // 用真实文件锁制造失败，验证主文件不会被部分写入破坏。
                store.Save(Data(.2));
                using (var held = new FileStream(Path.Combine(retry, "pet.json.tmp"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    bool failed = false;
                    try { store.Save(Data(.4)); } catch (IOException) { failed = true; }
                    Check(failed && Decode(File.ReadAllText(Path.Combine(retry, "pet.json"))).Familiarity == .2, "写入失败保护原文件", ref count);
                }
                store.Save(Data(.4));
                Check(Store(retry).Load(() => Data(0)).Familiarity == .4, "失败后可以重试", ref count);
                return count;
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        public static PetSaveData Data(double familiarity)
        { return new PetSaveData { Version = 1, Familiarity = familiarity }; }
        private static PetSaveStore Store(string directory) { return new PetSaveStore(directory, Decode, Encode); }
        private static string Encode(PetSaveData data)
        {
            return string.Join("|", new[] { data.Version.ToString(CultureInfo.InvariantCulture), data.Familiarity.ToString("R", CultureInfo.InvariantCulture),
                data.HeadPreference.ToString("R", CultureInfo.InvariantCulture) });
        }
        private static PetSaveData Decode(string value)
        {
            var fields = value.Split('|');
            if (fields.Length != 3) throw new FormatException();
            return new PetSaveData { Version = int.Parse(fields[0], CultureInfo.InvariantCulture), Familiarity = Parse(fields[1]),
                HeadPreference = Parse(fields[2]) };
        }
        private static double Parse(string value) { return double.Parse(value, CultureInfo.InvariantCulture); }
        private static void Check(bool value, string message, ref int count)
        { if (!value) throw new InvalidOperationException(message); count++; }
    }
}
