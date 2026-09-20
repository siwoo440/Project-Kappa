#if UNITY_EDITOR // Map 생성 설정 창
using System.IO; // 기존 Map 파일 확인
using UnityEditor; // 수동 실행 메뉴
using UnityEditor.SceneManagement; // 기존 Map 열기
using UnityEngine; // 간단한 설정 화면

namespace ProjectK.Day16 // 본편 Map 전용 이름 공간
{
    public sealed class MapBuildWindow : EditorWindow // 타일 크기를 생성 전에 결정하는 창
    {
        private int tileSize = 512; // 기본 한 타일 길이
        private int heightResolution = 257; // 기본 높이 표본 해상도
        private int seed = 1616; // 반복 가능한 배치 시드
        private bool addToBuild = true; // 기존 빌드 목록 끝에만 Map 추가
        private static readonly string[] TileLabels = new string[] // 타일 크기 표시
        {
            "256 × 256", // 작은 타일
            "512 × 512", // 기본 타일
            "1000 × 1000" // 큰 타일
        };
        private static readonly int[] TileValues = new int[] // 실제 미터 값
        {
            256, // 작은 타일 길이
            512, // 기본 타일 길이
            1000 // 큰 타일 길이
        };
        private static readonly string[] ResolutionLabels = new string[] // 높이 해상도 표시
        {
            "257 × 257", // 기본 격자
            "513 × 513" // 정밀 격자
        };
        private static readonly int[] ResolutionValues = new int[] // 실제 높이 표본 수
        {
            257, // 기본 표본
            513 // 정밀 표본
        };

        // 독립 월드 생성 진입
        public static void Open() // 설정 창 표시
        {
            MapBuildWindow window = GetWindow<MapBuildWindow>("Map / 3x3 Terrain"); // 단일 설정 창
            window.minSize = new Vector2(470f, 325f); // 내용이 잘리지 않는 최소 크기
            window.Show(); // 사용자에게 표시
        }

        private void OnGUI() // 생성 전 크기와 범위 표시
        {
            EditorGUILayout.LabelField("본편 Map 씬 · Terrain 3 × 3", EditorStyles.boldLabel); // 작업 대상 안내
            EditorGUILayout.Space(8f); // 설정 간격
            tileSize = EditorGUILayout.IntPopup("한 타일 크기 / m", tileSize, TileLabels, TileValues); // 사용자가 실제 길이 선택
            heightResolution = EditorGUILayout.IntPopup("높이 해상도", heightResolution, ResolutionLabels, ResolutionValues); // 지형 편집 정밀도 선택
            seed = EditorGUILayout.IntField("배치 시드", seed); // 재현할 도시 선택
            addToBuild = EditorGUILayout.Toggle("기존 빌드 목록 끝에 추가", addToBuild); // 기존 첫 씬은 변경하지 않음
            EditorGUILayout.Space(8f); // 결과 정보 간격
            EditorGUILayout.HelpBox("전체 크기: " + (tileSize * 3) + "m × " + (tileSize * 3) + "m\nTerrainData 9개 · 연결 도로 · 도시 외부 건물 · 옥상 거점\nTest는 유지하며 미션·상시 적·전체 건물 내부는 이번 범위에 포함하지 않습니다.", MessageType.Info); // 실제 범위 설명
            EditorGUILayout.HelpBox("열려 있는 씬을 먼저 저장하세요. 기존 Map.unity는 덮어쓰지 않습니다. 생성 후에는 타일과 건물을 Inspector에서 편집할 수 있습니다.", MessageType.None); // 사용자 자료 보존 조건
            bool exists = File.Exists(MapSceneBuilder.MapPath); // 기존 파일 존재 확인
            using (new EditorGUI.DisabledScope(exists || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)) // 안전한 실행 시점만 허용
            {
                if (GUILayout.Button("Map 씬 생성", GUILayout.Height(36f))) // 명시적 생성 실행
                {
                    MapSceneBuilder.Build(tileSize, heightResolution, seed, addToBuild); // 실제 에셋과 씬 생성
                }
            }
            if (exists) // 이미 Map이 있는 경우 안내
            {
                EditorGUILayout.HelpBox("이미 Map 씬이 있습니다. 이번 도구는 수동 편집을 보존하기 위해 다시 생성하지 않습니다.", MessageType.Warning); // 덮어쓰기 방지 안내
                if (GUILayout.Button("기존 Map 열기")) // 기존 결과로 이동
                {
                    if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) // 저장하지 않은 편집 보호
                    {
                        EditorSceneManager.OpenScene(MapSceneBuilder.MapPath, OpenSceneMode.Single); // 중복 플레이어 없는 단일 씬 열기
                    }
                }
            }
        }
    }
}
#endif
