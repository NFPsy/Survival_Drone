using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

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
