"""VARCO 3D가 내보낸 FBX를 Unity가 읽을 수 있는 FBX로 다시 내보내는 Blender 스크립트.

왜 필요한가:
  VARCO 3D가 만든 FBX(리깅·동작 포함)는 Blender에서는 동작이 잘 읽히는데,
  Unity는 동작(Animation Clip)을 하나도 읽지 못했다. Blender로 가져왔다가 다시 내보내면 Unity가 읽는다.
  이때 그림(텍스처)을 파일 안에 넣지 않아서 파일 크기도 줄어든다(예: 24MB -> 0.4~1MB).
  그림은 프로젝트에 이미 있는 diffuse.png / normal.png를 Unity에서 재질로 연결해 쓴다.

사용법 (Blender를 화면 없이 실행):
  "C:\\Program Files\\Blender Foundation\\Blender 5.2\\blender.exe" -b --python export_for_unity.py -- <입력.fbx> <출력.fbx>

주의:
  - 동작이 들어 있는 FBX는 동작이 있는 구간(첫 키~마지막 키)만 내보낸다.
  - 모델이 바라보는 방향은 바꾸지 않는다. 동작의 방향을 돌려야 하면 export_rotated.py를 쓴다.
"""
import bpy
import sys

# "--" 뒤에 적은 값이 우리가 넘긴 입력 파일, 출력 파일이다.
src = sys.argv[sys.argv.index("--") + 1]
dst = sys.argv[sys.argv.index("--") + 2]

# 빈 장면에서 시작해서 입력 FBX를 가져온다.
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)

# 동작이 있으면 그 구간만 내보내도록 장면의 프레임 범위를 동작 길이에 맞춘다.
if bpy.data.actions:
    act = bpy.data.actions[0]
    start, end = int(act.frame_range[0]), int(act.frame_range[1])
    bpy.context.scene.frame_start = start
    bpy.context.scene.frame_end = end
    print("ACTION RANGE", start, end)

bpy.ops.export_scene.fbx(
    filepath=dst,
    object_types={'ARMATURE', 'MESH'},       # 뼈대와 모델만 (카메라·조명 제외)
    add_leaf_bones=False,                    # 끝 뼈를 새로 만들지 않는다 (Unity에서 군더더기 뼈가 생기는 것을 막음)
    bake_anim=bool(bpy.data.actions),        # 동작이 있을 때만 동작을 구워서 내보낸다
    bake_anim_use_all_actions=False,
    bake_anim_use_nla_strips=False,
    bake_anim_force_startend_keying=True,
    bake_anim_simplify_factor=0.0,           # 동작을 단순화하지 않는다 (원본 그대로)
    path_mode='STRIP',                       # 텍스처 경로를 파일 이름만 남긴다
    embed_textures=False,                    # 그림을 파일 안에 넣지 않는다 (용량이 크게 줄어드는 핵심 설정)
    apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z',
    axis_up='Y',
    use_armature_deform_only=False,
    mesh_smooth_type='FACE',
)
print("EXPORTED", dst)
