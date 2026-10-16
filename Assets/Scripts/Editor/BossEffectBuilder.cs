using UnityEditor;
using UnityEngine;

namespace SurvivalDrone.EditorTools
{
    // 보스전에 쓰는 재질(그림을 입히는 "표면 설정")을 만들어 주는 에디터 도구.
    // 메뉴 SurvivalDrone → Effects → Build Boss Materials 로 실행한다. 여러 번 눌러도 같은 파일을 다시 맞출 뿐이라 안전하다.
    //
    // 만드는 것(Assets/Resources 폴더):
    //  - BossArenaRing.mat : 보스전 제한 테두리(바닥의 붉은 원)와 공격 예고의 테두리를 그리는 재질. 붉은색 반투명.
    //  - BossTelegraphFill.mat : 공격 예고(내려찍기 원·돌진 선)의 속을 칠하는 재질. 더 투명한 붉은색.
    //
    // Resources 폴더에 두는 이유: 웹 빌드는 "씬이 직접 쓰는 재질"만 빌드에 넣고, 코드가 나중에 불러 쓰는 재질은 빠뜨릴 수 있다.
    // Resources 폴더 안의 에셋은 항상 빌드에 들어가고, 그 재질이 쓰는 셰이더(그리는 방식)도 같이 따라 들어간다.
    // 새 효과 재질이 필요하면 기존 효과 재질(Mat_ExplosionAimRing)을 본떠서 만든다. 이미 웹에서 잘 보이는 것이 확인된 재질이라서다.
    public static class BossEffectBuilder
    {
        private const string TemplateMaterialPath = "Assets/Materials/Mat_ExplosionAimRing.mat";
        private const string RingMaterialPath = "Assets/Resources/BossArenaRing.mat";
        private const string TelegraphFillPath = "Assets/Resources/BossTelegraphFill.mat";

        [MenuItem("SurvivalDrone/Effects/Build Boss Materials")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogWarning("[BossEffect] 플레이 중이거나 컴파일 중에는 만들 수 없습니다. 멈춘 뒤 다시 실행하세요.");
                return;
            }

            // 붉은색, 약간 투명한 고리. (바닥이 어두워서 이 정도 밝기면 멀리서도 보인다)
            CreateAlphaMaterial(RingMaterialPath, new Color(1f, 0.2f, 0.14f, 0.85f));

            // 공격 예고(바닥에 깔리는 붉은 원·선)의 속을 칠하는 재질. 많이 투명해서 겹쳐 칠해도 바닥이 비친다.
            CreateAlphaMaterial(TelegraphFillPath, new Color(1f, 0.16f, 0.1f, 0.32f));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[BossEffect] 보스전 재질을 만들었습니다: " + RingMaterialPath);
        }

        // 기존 효과 재질을 본떠서 "일반 반투명" 재질을 만들고 색을 입힌다. 그림(텍스처)은 쓰지 않는다(단색).
        private static Material CreateAlphaMaterial(string path, Color color)
        {
            var template = AssetDatabase.LoadAssetAtPath<Material>(TemplateMaterialPath);
            if (template == null)
            {
                Debug.LogError("[BossEffect] 본뜰 재질을 찾지 못했습니다: " + TemplateMaterialPath);
                return null;
            }

            System.IO.Directory.CreateDirectory("Assets/Resources");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(template);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.CopyPropertiesFromMaterial(template);
            }

            material.SetFloat("_Blend", 0f);          // 0 = 일반 투명(Alpha), 2 = 겹쳐 밝아지기(Additive)
            material.SetFloat("_SrcBlend", 5f);       // 소스 투명도
            material.SetFloat("_DstBlend", 10f);      // 1 - 소스 투명도
            material.SetFloat("_SrcBlendAlpha", 1f);
            material.SetFloat("_DstBlendAlpha", 10f);
            material.SetTexture("_BaseMap", null);
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
