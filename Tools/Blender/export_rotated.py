"""동작의 몸 방향을 수직축(위쪽 축)으로 돌려서 Unity용 FBX로 내보내는 Blender 스크립트.

왜 필요한가:
  VARCO 3D가 만든 달리기 동작은 캐릭터가 오른쪽(+X)을 보는 자세였고, 대기 동작은 앞(+Z)을 보는 자세였다.
  게임 코드는 캐릭터 전체를 이동 방향으로 돌리는데, 동작 자체도 이미 90도 돌아가 있어서
  D(오른쪽)로 달리면 두 회전이 합쳐져 캐릭터가 엉뚱한 방향(아래)을 봤다.
  그래서 달리기 동작의 몸 전체를 수직축으로 돌려서 앞을 보게 만든 FBX를 다시 내보냈다.

사용법 (Blender를 화면 없이 실행):
  "C:\\Program Files\\Blender Foundation\\Blender 5.2\\blender.exe" -b --python export_rotated.py -- <입력.fbx> <출력.fbx> <각도>
  예) ... -- Run.fbx Player_Run.fbx 80      (달리기: 몸 방향 평균이 앞(+Z)이 되도록 80도)

각도 정하는 법:
  먼저 Unity에서 동작의 몸 방향을 잰다(어깨선으로 "앞쪽"을 구해 yaw 각도를 계산).
  대기 동작은 0도(앞), 달리기 동작은 약 88도였다. 달리기를 돌리고 다시 재서 평균이 0에 가깝게 각도를 맞춘다.
  (90도로 돌렸을 때 평균이 약 -10도였고, 80도로 돌리자 평균 +1.6도가 되었다.)
  부호가 맞는지는 반대 부호로도 한 번 시험해서 비교한다.

중요한 함정:
  맨 위 뼈(Root)에도 프레임마다 값이 이미 들어 있다. 맨 위 뼈에 "고정 회전"만 주면
  프레임마다 있는 값이 덮어써서 아무 효과가 없다.
  그래서 모든 프레임의 값을 먼저 계산해 두고, 프레임마다 새 값을 "키"로 다시 넣는다.
"""
import bpy
import sys
import math
import mathutils

args = sys.argv[sys.argv.index("--") + 1:]
src, dst, angle_deg = args[0], args[1], float(args[2])

# 빈 장면에서 시작해서 입력 FBX를 가져온다.
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)

scene = bpy.context.scene
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
bpy.context.view_layer.objects.active = arm

# 맨 위 뼈(부모가 없는 뼈)를 찾는다. 이 뼈를 돌리면 아래 뼈가 모두 따라 돈다.
root = [b for b in arm.data.bones if b.parent is None][0]
pb = arm.pose.bones[root.name]
ml = root.matrix_local.copy()
R = mathutils.Matrix.Rotation(math.radians(angle_deg), 4, 'Z')   # Blender는 Z가 위쪽
offset = ml.inverted() @ R @ ml

act = bpy.data.actions[0]
start, end = int(act.frame_range[0]), int(act.frame_range[1])
scene.frame_start = start
scene.frame_end = end

# 1) 키를 덮어쓰기 전에, 모든 프레임의 새 값을 먼저 계산해 둔다.
news = {}
for f in range(start, end + 1):
    scene.frame_set(f)
    news[f] = offset @ pb.matrix_basis.copy()
print("ROOT", root.name, "rotation_mode", pb.rotation_mode, "frames", start, end)

# 2) 계산한 값을 같은 프레임의 키로 다시 넣는다.
rot_path = 'rotation_quaternion' if pb.rotation_mode == 'QUATERNION' else 'rotation_euler'
for f in range(start, end + 1):
    scene.frame_set(f)
    pb.matrix_basis = news[f]
    pb.keyframe_insert('location', frame=f)
    pb.keyframe_insert(rot_path, frame=f)

bpy.ops.export_scene.fbx(
    filepath=dst,
    object_types={'ARMATURE', 'MESH'},
    add_leaf_bones=False,
    bake_anim=True,
    bake_anim_use_all_actions=False,
    bake_anim_use_nla_strips=False,
    bake_anim_use_all_bones=True,
    bake_anim_force_startend_keying=True,
    bake_anim_simplify_factor=0.0,
    path_mode='STRIP',
    embed_textures=False,
    apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z',
    axis_up='Y',
    use_armature_deform_only=False,
    mesh_smooth_type='FACE',
)
print("EXPORTED", dst)
