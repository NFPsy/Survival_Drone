# Blender FBX 변환 도구

VARCO 3D에서 받은 **뼈대(리깅)·동작이 들어 있는 FBX**를 Unity에서 쓰려고 만든 Blender 스크립트 모음이다.
(주인공 모델의 대기·달리기 동작을 적용할 때 쓴 것, 2026-10-11)

## 왜 필요했나

- VARCO가 내보낸 FBX는 Blender에서는 동작이 읽히는데 **Unity는 동작(Clip)을 하나도 읽지 못했다.**
- Blender로 가져왔다가 다시 내보내면 Unity가 읽는다.
- 다시 내보낼 때 그림(텍스처)을 파일 안에 넣지 않으면 파일 하나가 약 24MB에서 0.4~1MB로 줄어든다.
  (그림은 프로젝트에 있는 `diffuse.png`·`normal.png`를 재질로 연결해 쓴다.)

## 스크립트

| 파일 | 하는 일 |
|---|---|
| `export_for_unity.py` | FBX를 Unity가 읽는 FBX로 다시 내보낸다. 텍스처는 제외한다. |
| `export_rotated.py` | 위와 같이 내보내되, 동작의 몸 전체를 수직축으로 지정한 각도만큼 돌린다. 동작이 바라보는 방향이 코드의 이동 방향과 어긋날 때 쓴다. |

## 사용법

Blender가 설치되어 있으면 화면 없이 명령어로 실행한다. (Blender 5.2에서 확인했다. 설치 위치가 다르면 경로를 바꾼다.)

```bash
"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b --python Tools/Blender/export_for_unity.py -- 입력.fbx 출력.fbx
"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b --python Tools/Blender/export_rotated.py -- 입력.fbx 출력.fbx 80
# 동작 일부만 내보내기: 각도 뒤에 시작·끝 프레임(Blender 프레임 번호)을 붙인다. (보스 내려찍기: -12도, 60~135프레임)
"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b --python Tools/Blender/export_rotated.py -- 입력.fbx 출력.fbx -12 60 135
```

`--` 뒤의 값이 입력 파일, 출력 파일(그리고 돌릴 각도)이다.

## Unity에 가져온 뒤 해야 할 설정

1. **재질**: FBX의 Import Settings → Materials에서 기존 `material_0.mat`을 연결한다(그림이 파일에 없어서 필요).
2. **아바타**: 대기 FBX가 아바타를 만들고(Create From This Model), 달리기·리깅 FBX는 Copy From Other Avatar로 그 아바타를 쓴다. 뼈대 구조가 같아야 한다.
3. **동작 이름과 반복**: Animation 탭에서 동작 이름을 `Idle`/`Run`으로 바꾸고 Loop Time을 켠다.
4. **방향 확인**: 대기 자세만이 아니라 **각 동작의 자세에서 몸이 바라보는 방향**을 재서 앞(+Z)인지 확인한다.

## 배운 점 (함정)

- **동작마다 바라보는 방향이 다를 수 있다.** 달리기 동작이 오른쪽(+X)을 보고 있어서 D로 달리면 아래를 보는 문제가 있었다.
  몸 전체를 80도 돌려 해결했다. 각도는 Unity에서 어깨선으로 "앞쪽"을 구해 yaw로 재서 정했다(평균이 0에 가까워질 때까지).
- **맨 위 뼈(Root)에 고정 회전만 주면 효과가 없다.** 맨 위 뼈에도 프레임마다 값이 있어서 덮어써진다.
  모든 프레임의 값을 먼저 계산하고 프레임마다 새 값을 키로 다시 넣어야 한다.
- **긴 동작은 필요한 구간만 잘라 쓴다.** 보스 내려찍기 원본은 228프레임인데 뒤쪽은 엎드린 채 굳은 자세라, "준비 → 바닥을 치는 순간"인 60~135프레임만 내보냈다. 바닥을 치는 프레임은 손·엉덩이 높이를 프레임마다 뽑아서 찾았다. 내보낸 파일을 Blender로 다시 열면 구간이 1프레임씩 밀려(60~135 → 61~136) 보이는데, Unity의 클립 구간(First/Last Frame)은 거기서 1을 뺀 값, 즉 처음에 준 60~135를 그대로 쓰면 맞다.
- **원본 FBX는 저장소에 넣지 않는다.** 텍스처가 들어 있어 24MB씩이다. 변환한 FBX(0.4~1MB)만 저장소에 둔다.
