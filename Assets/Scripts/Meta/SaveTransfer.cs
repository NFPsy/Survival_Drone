using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;
using SurvivalDrone.Drones;

namespace SurvivalDrone.Meta
{
    // 저장 슬롯 하나를 "글자 코드"로 바꾸고(내보내기), 그 코드를 다시 저장으로 되돌리는(불러오기) 도우미.
    //
    // 왜 만들었나:
    //  웹 브라우저 게임의 저장은 그 브라우저 안(게임 주소별 저장 공간)에만 있다. 게임을 새로 올려서 주소가 바뀌면
    //  테스터의 저장이 사라진다. 그래서 업데이트 전에 코드를 복사해 두었다가 새 버전에서 붙여 넣어 이어서 할 수 있게 했다.
    //  서버나 로그인이 필요 없다.
    //
    // 코드 모양:  "SDSAVE1:" + (저장 JSON을 압축(GZip)한 뒤 Base64 글자로 바꾼 것)
    //  - 앞의 "SDSAVE1:"은 이 게임의 저장 코드라는 표시이자 코드 형식 번호다.
    //  - 압축 덕분에 코드가 짧아지고, GZip 안에 들어 있는 검사값 때문에 중간에 글자가 빠지거나 바뀐 코드는 불러올 때 걸러진다.
    //  - 테스트 기록 문장(playLog)은 코드에 넣지 않는다. 길이가 너무 길어지기 때문이다. (기록은 로비의 "테스트 로그"로 따로 보낸다)
    //
    // 주의: 코드는 사람이 열어서 값을 고칠 수도 있다. 이 게임은 결제가 없는 데모라 막지 않는다.
    public static class SaveTransfer
    {
        public const string Prefix = "SDSAVE1:";

        // 이 게임이 읽을 수 있는 저장 형식 번호의 최대값. 이보다 큰 번호의 코드는 더 새 버전에서 만든 것이라 거절한다.
        public const int MaxSupportedSaveVersion = 1;

        // 압축을 푼 결과가 이 크기를 넘으면 거절한다. (이상한 코드가 메모리를 잡아먹지 못하게)
        private const int MaxJsonBytes = 2 * 1024 * 1024;

        public enum DecodeResult
        {
            Ok,
            Empty,       // 아무것도 붙여 넣지 않음
            NotACode,    // 이 게임의 저장 코드가 아님 (앞부분 표시가 없음)
            Corrupt,     // 코드가 중간에 잘리거나 바뀜
            TooNew,      // 더 새 버전에서 만든 코드
        }

        // 저장 데이터를 코드로 바꾼다. 원본 data는 바꾸지 않는다.
        public static string Encode(SaveData data)
        {
            // 복사본을 만들어서 기록 문장만 비운다.
            var copy = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
            copy.playLog = new List<string>();

            byte[] raw = Encoding.UTF8.GetBytes(JsonUtility.ToJson(copy));
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, CompressionMode.Compress, true))
                {
                    gzip.Write(raw, 0, raw.Length);
                }
                return Prefix + Convert.ToBase64String(output.ToArray());
            }
        }

        // 코드를 저장 데이터로 되돌린다. 성공하면 Ok와 data, 아니면 이유와 null.
        // 붙여 넣을 때 섞여 들어온 공백·줄바꿈·따옴표는 무시한다.
        public static DecodeResult TryDecode(string code, out SaveData data)
        {
            data = null;
            string cleaned = Clean(code);
            if (cleaned.Length == 0) return DecodeResult.Empty;
            if (!cleaned.StartsWith(Prefix, StringComparison.Ordinal)) return DecodeResult.NotACode;

            string json;
            try
            {
                byte[] compressed = Convert.FromBase64String(cleaned.Substring(Prefix.Length));
                using (var input = new MemoryStream(compressed))
                using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    var buffer = new byte[8192];
                    int read;
                    while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        output.Write(buffer, 0, read);
                        if (output.Length > MaxJsonBytes) return DecodeResult.Corrupt;
                    }
                    json = Encoding.UTF8.GetString(output.ToArray());
                }
            }
            catch (Exception)
            {
                return DecodeResult.Corrupt;
            }

            SaveData parsed;
            try
            {
                parsed = JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception)
            {
                return DecodeResult.Corrupt;
            }

            if (parsed == null || parsed.saveVersion < 1 || parsed.core < 0 || parsed.credit < 0) return DecodeResult.Corrupt;
            if (parsed.saveVersion > MaxSupportedSaveVersion) return DecodeResult.TooNew;

            data = parsed;
            return DecodeResult.Ok;
        }

        // ---------------- 값 범위 조정 ----------------
        // 코드는 사람이 열어서 값을 고칠 수 있으므로, 불러오기 전에 "게임이 실제로 만들 수 있는 범위"로 값을 맞춘다.
        // 거절하지 않고 조정하는 이유: 정상 저장이 실수로 거절되는 일이 없게 하려는 것이다. (한도는 정상 플레이로 도달하는 값보다 넉넉하다)
        // 정상 저장은 한 항목도 조정되지 않아야 한다. (검증 메뉴가 실제 저장 파일로 확인한다)

        // 게임 규칙에서 오는 한도. 게임이 켜져 있으면 FromGame()이 실제 값을 가져오고, 아니면 기본값을 쓴다.
        public class Limits
        {
            public int maxDroneLevel = 8;      // 강화 5 + 각성 3
            public int equipSlots = 2;         // 장착 칸 수
            public int stageCount = 10;        // 노멀 5 + 하드 5
            public int normalStageCount = 5;

            public static Limits FromGame()
            {
                var limits = new Limits();
                var inventory = DroneInventory.Instance;
                if (inventory != null)
                {
                    if (inventory.MaxLevel > 1) limits.maxDroneLevel = inventory.MaxLevel;
                    if (inventory.EquipSlotCount > 0) limits.equipSlots = inventory.EquipSlotCount;
                }
                var stages = StageProgress.Instance;
                if (stages != null)
                {
                    if (stages.StageCount > 0) limits.stageCount = stages.StageCount;
                    if (stages.NormalStageCount > 0) limits.normalStageCount = stages.NormalStageCount;
                }
                return limits;
            }
        }

        private const int MaxCurrency = 99999999;        // 코어·크레딧 상한 (정상 플레이로는 닿지 않는 값)
        private const int MaxShards = 999999;            // 드론 한 종의 조각 상한
        private const int MaxCounter = 1000;             // 천장·소천장·조각 교환 횟수 같은 세는 값의 상한
        private const float MaxSurviveSeconds = 7200f;   // 스테이지 최고 생존 시간 상한(초)
        private const int MaxBitMask = 255;              // 마일스톤·퀘스트 비트 값 상한

        // 저장 데이터의 값을 게임이 만들 수 있는 범위로 맞춘다. 바꾼 항목의 개수를 돌려주고, notes에 무엇을 바꿨는지 짧게 적는다.
        // data는 직접 고친다. (불러오기 직전의 새 데이터에만 쓴다)
        public static int Sanitize(SaveData data, Limits limits, out List<string> notes)
        {
            // 람다 안에서는 out 매개변수를 쓸 수 없어서, 같은 목록을 지역 변수로도 들고 있는다.
            var noteList = new List<string>();
            notes = noteList;
            if (data == null) return 0;
            if (limits == null) limits = new Limits();
            int count = 0;

            // 숫자 하나를 범위 안으로 맞춘다.
            Func<string, int, int, int, int> clampInt = (label, value, min, max) =>
            {
                int clamped = Mathf.Clamp(value, min, max);
                if (clamped != value) { count++; noteList.Add($"{label} {value:N0}→{clamped:N0}"); }
                return clamped;
            };

            data.saveVersion = Mathf.Max(1, data.saveVersion);
            data.core = clampInt("코어", data.core, 0, MaxCurrency);
            data.credit = clampInt("크레딧", data.credit, 0, MaxCurrency);
            data.gachaPityCount = clampInt("천장 횟수", data.gachaPityCount, 0, MaxCounter);
            data.gachaSoftPityCount = clampInt("소천장 횟수", data.gachaSoftPityCount, 0, MaxCounter);
            data.shardExchangeCount = clampInt("조각 교환 횟수", data.shardExchangeCount, 0, MaxCounter);
            data.unlockedStageCount = clampInt("해금 스테이지", data.unlockedStageCount, 1, Mathf.Max(1, limits.normalStageCount));
            data.normalClearedMask = clampInt("노멀 클리어 기록", data.normalClearedMask, 0, (1 << Mathf.Max(1, limits.normalStageCount)) - 1);
            data.milestoneDoneMask = clampInt("마일스톤 기록", data.milestoneDoneMask, 0, MaxBitMask);
            data.milestoneClaimedMask = clampInt("마일스톤 수령", data.milestoneClaimedMask, 0, MaxBitMask);
            data.dailyQuestDoneMask = clampInt("퀘스트 기록", data.dailyQuestDoneMask, 0, MaxBitMask);
            data.dailyQuestClaimedMask = clampInt("퀘스트 수령", data.dailyQuestClaimedMask, 0, MaxBitMask);
            data.importCount = clampInt("불러온 횟수", data.importCount, 0, MaxCounter);

            // 글자 값은 길이만 제한한다. (null은 빈 글자로)
            Func<string, int, string> limitText = (value, max) =>
            {
                if (value == null) return "";
                if (value.Length <= max) return value;
                count++;
                noteList.Add("글자 값이 너무 길어 잘림");
                return value.Substring(0, max);
            };
            data.testerId = limitText(data.testerId, 12);
            data.lastSavedAt = limitText(data.lastSavedAt, 32);
            data.dailyQuestDate = limitText(data.dailyQuestDate, 16);
            data.shardExchangeDate = limitText(data.shardExchangeDate, 16);

            // 목록 값
            if (data.logStats == null) data.logStats = new PlayLogStats();
            if (data.playLog == null) data.playLog = new List<string>();
            data.stageMilestoneDoneMasks = SanitizeMasks(data.stageMilestoneDoneMasks, limits.stageCount, ref count, noteList);
            data.stageMilestoneClaimedMasks = SanitizeMasks(data.stageMilestoneClaimedMasks, limits.stageCount, ref count, noteList);

            if (data.bestSurvivalSeconds == null) data.bestSurvivalSeconds = new List<float>();
            if (data.bestSurvivalSeconds.Count > limits.stageCount)
            {
                count++;
                noteList.Add("최고 기록 칸 수 줄임");
                data.bestSurvivalSeconds.RemoveRange(limits.stageCount, data.bestSurvivalSeconds.Count - limits.stageCount);
            }
            for (int i = 0; i < data.bestSurvivalSeconds.Count; i++)
            {
                float value = data.bestSurvivalSeconds[i];
                float fixedValue = float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp(value, 0f, MaxSurviveSeconds);
                if (!Mathf.Approximately(fixedValue, value)) { count++; noteList.Add($"스테이지 {i + 1} 최고 기록 조정"); data.bestSurvivalSeconds[i] = fixedValue; }
            }

            // 보유 드론: 실제 있는 종류만, 종류당 하나, 등급·레벨·조각은 범위 안으로.
            if (data.ownedDrones == null) data.ownedDrones = new List<OwnedDroneData>();
            var seen = new HashSet<int>();
            var kept = new List<OwnedDroneData>();
            foreach (var drone in data.ownedDrones)
            {
                if (drone == null || !Enum.IsDefined(typeof(DroneType), drone.droneType) || !seen.Add((int)drone.droneType))
                {
                    count++;
                    noteList.Add("잘못된 드론 항목 제거");
                    continue;
                }
                if (!Enum.IsDefined(typeof(GachaRarity), drone.rarity)) { count++; noteList.Add("드론 등급 조정"); drone.rarity = GachaRarity.N; }
                drone.level = clampInt("드론 레벨", drone.level, 1, Mathf.Max(1, limits.maxDroneLevel));
                drone.shards = clampInt("드론 조각", drone.shards, 0, MaxShards);
                kept.Add(drone);
            }
            data.ownedDrones = kept;

            // 장착: 칸 수를 넘는 것은 자르고, 가지고 있지 않거나 겹치는 드론은 빈 칸(-1)으로.
            if (data.equippedDrones == null) data.equippedDrones = new List<int>();
            if (data.equippedDrones.Count > limits.equipSlots)
            {
                count++;
                noteList.Add("장착 칸 수 줄임");
                data.equippedDrones.RemoveRange(limits.equipSlots, data.equippedDrones.Count - limits.equipSlots);
            }
            var equippedSeen = new HashSet<int>();
            for (int i = 0; i < data.equippedDrones.Count; i++)
            {
                int type = data.equippedDrones[i];
                if (type == -1) continue;
                if (!seen.Contains(type) || !equippedSeen.Add(type))
                {
                    count++;
                    noteList.Add("장착 드론 정리");
                    data.equippedDrones[i] = -1;
                }
            }

            return count;
        }

        private static List<int> SanitizeMasks(List<int> masks, int maxCount, ref int count, List<string> notes)
        {
            if (masks == null) return new List<int>();
            if (masks.Count > maxCount)
            {
                count++;
                notes.Add("마일스톤 칸 수 줄임");
                masks.RemoveRange(maxCount, masks.Count - maxCount);
            }
            for (int i = 0; i < masks.Count; i++)
            {
                int fixedValue = Mathf.Clamp(masks[i], 0, MaxBitMask);
                if (fixedValue != masks[i]) { count++; notes.Add("마일스톤 기록 조정"); masks[i] = fixedValue; }
            }
            return masks;
        }

        // 화면에 보여 줄 실패 이유 (한국어).
        public static string Describe(DecodeResult result)
        {
            switch (result)
            {
                case DecodeResult.Empty: return "붙여 넣은 내용이 없습니다.";
                case DecodeResult.NotACode: return "저장 코드가 아닙니다. \"SDSAVE1:\"로 시작하는 코드를 통째로 붙여 넣어 주세요.";
                case DecodeResult.Corrupt: return "코드가 깨졌습니다. 중간에 잘리거나 글자가 빠지지 않았는지 확인하고 다시 복사해 주세요.";
                case DecodeResult.TooNew: return "더 새 버전에서 만든 코드라서 이 버전에서는 읽을 수 없습니다.";
                default: return "";
            }
        }

        // 확인 창에 보여 줄 한 줄 요약. (불러오기 전에 "이 저장이 맞는지" 눈으로 확인하게 하려는 것)
        public static string Summarize(SaveData data)
        {
            if (data == null) return "(저장 없음)";
            int drones = data.ownedDrones != null ? data.ownedDrones.Count : 0;
            return $"코어 {data.core:N0} · 크레딧 {data.credit:N0} · 드론 {drones}종 · 해금 스테이지 {Mathf.Max(1, data.unlockedStageCount)}개";
        }

        private static string Clean(string code)
        {
            if (string.IsNullOrEmpty(code)) return "";
            var builder = new StringBuilder(code.Length);
            foreach (char c in code)
            {
                if (char.IsWhiteSpace(c) || c == '"' || c == '\'' || c == '`') continue;
                builder.Append(c);
            }
            return builder.ToString();
        }
    }
}
