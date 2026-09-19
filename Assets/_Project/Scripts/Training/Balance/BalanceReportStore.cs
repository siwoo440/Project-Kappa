using System; // 고유 파일 이름과 날짜
using System.Collections.Generic; // 결과 목록 저장
using System.Globalization; // 지역과 무관한 숫자 표기
using System.IO; // 로컬 시험 기록 저장
using System.Security.Cryptography; // 무기 설정 변경 구분
using System.Text; // 한글과 CSV 문자열 처리
using UnityEngine; // 실행별 저장 경로와 JSON

[Serializable] // 순수 값으로 저장하는 총기 기준
public sealed class BalanceWeaponSnapshot // 실행 인스턴스 번호와 무관한 설정 기록
{
    public string stats; // 공통 피해와 탄약 설정 JSON
    public string handling; // 반동과 분산 설정 JSON
    public string definitionName; // 원본 총기 에셋 이름
    public int fireMode; // 반자동과 자동 구분
    public int pelletCount; // 한 발 펠릿 수
    public float headDamage; // 머리 전체 피해
    public float reloadDuration; // 탄창 또는 삽입 시간
    public float maximumRange; // 발사 검사 최대 거리
    public float endDamageRatio; // 최대 거리의 피해 배율
    public float equipDuration; // 장착 시간
    public float aimDuration; // 조준 시간
    public float aimFovRatio; // 실제 조준 배율
    public float movementMultiplier; // 장착 이동 배율
    public float cycleDuration; // 펌프와 볼트 준비 시간
    public bool singleRound; // 삽입식 장전 여부
    public bool bolt; // 볼트 여부
    public bool scope; // 저격 표시 여부
    public bool suppressed; // 소음기 상태
}

public static class BalanceReportStore // 게임 에셋과 분리된 시험 결과 저장
{
    public static string DirectoryPath => Path.Combine(Application.persistentDataPath, "BalanceReports"); // 빌드에서도 쓸 수 있는 개인 결과 폴더

    public static string Snapshot(FirearmDefinition definition, bool suppressed) // 실행 참조가 없는 비교용 설정
    {
        if (definition == null || definition.Stats == null) // 유효한 총기 확인
        {
            return ""; // 미완성 총기 제외
        }
        BalanceWeaponSnapshot data = new BalanceWeaponSnapshot(); // 값 자료 생성
        data.stats = JsonUtility.ToJson(definition.Stats); // 고정 피해와 탄약 값
        data.handling = definition.Handling != null ? JsonUtility.ToJson(definition.Handling) : ""; // 분산과 반동 값
        data.definitionName = definition.name; // 에셋 식별 이름
        data.fireMode = (int)definition.FireMode; // 실제 발사 방식
        data.pelletCount = definition.PelletCount; // 실제 펠릿 수
        data.headDamage = definition.HeadDamage; // 실제 머리 피해
        data.reloadDuration = definition.ReloadDuration; // 실제 장전 시간
        data.maximumRange = definition.MaximumRange; // 실제 발사 한계
        data.endDamageRatio = definition.DamageMultiplier(definition.MaximumRange); // 실제 감쇠 끝값
        data.equipDuration = definition.EquipDuration; // 실제 장착 시간
        data.aimDuration = definition.AimDuration; // 실제 조준 시간
        data.aimFovRatio = definition.AimFovRatio; // 실제 조준 시야각 비율
        data.movementMultiplier = definition.MovementMultiplier; // 실제 이동 배율
        data.cycleDuration = definition.CycleDuration; // 실제 기구 준비 시간
        data.singleRound = definition.SingleRoundReload; // 실제 장전 방식
        data.bolt = definition.BoltAction; // 볼트 표시 구분
        data.scope = definition.ScopeEnabled; // 조준경 사용 여부
        data.suppressed = suppressed; // 현재 소음기 사용 여부
        return JsonUtility.ToJson(data); // 순수 설정만 직렬화
    }

    public static string Hash(string value) // 같은 조건끼리 묶는 설정 지문
    {
        using (SHA256 hash = SHA256.Create()) // 고정 길이 해시 도구
        {
            byte[] bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(value ?? "")); // 한글을 포함한 조건 계산
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant(); // 파일과 결과에서 동일한 표기
        }
    }

    public static string TargetFingerprint(TrainingCenterLane lane) // 표적 크기와 기준 위치가 바뀐 결과 분리
    {
        if (lane == null || lane.Target == null || lane.Target.Hinge == null || lane.FiringPoint == null) // 필수 표적 자료 확인
        {
            return "missing"; // 불완전한 조건 구분
        }
        StringBuilder data = new StringBuilder(); // 단순한 비교용 형상 자료
        Vector3 relative = lane.Target.transform.position - lane.FiringPoint.position; // 실제 기준 거리와 방향
        Vector3 scale = lane.Target.transform.lossyScale; // 표적 실제 크기 배율
        data.Append(Number(relative.x)).Append('|').Append(Number(relative.y)).Append('|').Append(Number(relative.z)); // 기준점 차이 저장
        data.Append('|').Append(Number(scale.x)).Append('|').Append(Number(scale.y)).Append('|').Append(Number(scale.z)); // 크기 배율 저장
        foreach (Collider hit in lane.Target.Hinge.GetComponentsInChildren<Collider>(true)) // 회전축의 실제 피격 형상 확인
        {
            if (hit.name.StartsWith("Day11_Hit_", StringComparison.Ordinal)) // 임시 탄착 장식 제외
            {
                continue; // 사격할 때마다 조건이 바뀌지 않도록 처리
            }
            Vector3 position = hit.transform.localPosition; // 움직이는 받침과 무관한 부위 위치
            data.Append('|').Append(hit.name).Append('|').Append(Number(position.x)).Append('|').Append(Number(position.y)).Append('|').Append(Number(position.z)); // 부위 위치 저장
            BoxCollider box = hit as BoxCollider; // 현재 표적의 실제 상자 피격 영역
            if (box != null) // 상자 크기 확인
            {
                data.Append('|').Append(Number(box.size.x)).Append('|').Append(Number(box.size.y)).Append('|').Append(Number(box.size.z)); // 머리와 몸통 크기 기록
            }
        }
        return Hash(data.ToString()).Substring(0, 16); // 짧고 안정적인 형상 구분자
    }

    public static string Cell(string value) // CSV의 쉼표와 줄바꿈 보존
    {
        return "\"" + (value ?? "").Replace("\"", "\"\"") + "\""; // 문자열 전체를 따옴표로 감싸기
    }

    public static string Number(double value) // 지역 설정 독립 숫자 출력
    {
        return value.ToString("0.######", CultureInfo.InvariantCulture); // 소수점 표기 통일
    }

    public static string TrialCsv(BalanceTrialRecord r) // 하나의 시험 결과 표
    {
        string header = "id,weapon,profile,status,reason,shots,hit_shots,shot_accuracy_percent,pellets,hit_pellets,pellet_accuracy_percent,head_pellets,actual_damage,ttk_seconds,elapsed_seconds,reloads,reload_seconds,max_frame_seconds,settings_hash"; // 열 이름
        string[] row = new string[] // 동일 순서의 결과 값
        {
            Cell(r.id), // 시험 고유 번호
            Cell(r.weaponName), // 무기 표시 이름
            Cell(r.profileKey), // 같은 시험 조건
            Cell(r.status), // 완료와 중단 상태
            Cell(r.reason), // 상세 종료 이유
            Number(r.shots), // 실제 발사 수
            Number(r.hitShots), // 명중한 발사 수
            Number(BalanceTrialMetrics.Percent(r.hitShots, r.shots)), // 발사 명중률
            Number(r.pellets), // 발사 펠릿 수
            Number(r.hitPellets), // 적중 펠릿 수
            Number(BalanceTrialMetrics.Percent(r.hitPellets, r.pellets)), // 펠릿 명중률
            Number(r.headPellets), // 머리 펠릿 수
            Number(r.actualDamage), // 실제 감소 피해
            r.status == "Completed" ? Number(r.ttk) : "", // 미완료 TTK는 빈칸
            Number(r.elapsed), // 중단까지 포함한 경과 시간
            Number(r.reloads), // 관측한 장전 횟수
            Number(r.reloadSeconds), // 관측한 장전 시간
            Number(r.maximumFrameSeconds), // 프레임 오차 판단 자료
            Cell(r.weaponFingerprint) // 사용한 설정 버전
        };
        return header + "\n" + string.Join(",", row) + "\n"; // 결과 파일 내용
    }

    public static string SaveTrial(BalanceTrialRecord record) // 한 시험마다 별도 결과 파일 생성
    {
        Directory.CreateDirectory(DirectoryPath); // 저장 가능한 디렉터리 확보
        string stem = "trial_" + record.id; // 사용자 입력이 없는 안전한 이름
        string path = Path.Combine(DirectoryPath, stem + ".json"); // 상세 기록 경로
        WriteNew(path, JsonUtility.ToJson(record, true)); // 조건과 발사별 자료 저장
        WriteNew(Path.Combine(DirectoryPath, stem + ".csv"), TrialCsv(record)); // 빠른 비교용 표 저장
        return path; // 실제 기록 위치 반환
    }

    public static string SaveObservation(BalanceObservationRecord record) // 실제 경비의 별도 관측 자료 저장
    {
        Directory.CreateDirectory(DirectoryPath); // 결과 폴더 확보
        string path = Path.Combine(DirectoryPath, "observation_" + record.id + ".json"); // 통제 실험과 다른 파일 이름
        WriteNew(path, JsonUtility.ToJson(record, true)); // 실제 시야와 소음 이벤트 저장
        return path; // 저장 경로 반환
    }

    private static void WriteNew(string path, string text) // 기존 결과를 덮어쓰지 않는 저장
    {
        using (FileStream stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) // 기존 이름 충돌 시 중단
        using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(true))) // 한글 읽기를 위한 UTF8 표시
        {
            writer.Write(text); // 준비된 결과만 기록
        }
    }

    public static List<BalanceTrialRecord> LoadRecent(int limit) // 이전 Play에서 저장한 시험도 비교
    {
        List<BalanceTrialRecord> records = new List<BalanceTrialRecord>(); // 유효한 시험 목록
        if (!Directory.Exists(DirectoryPath)) // 최초 실행 여부 확인
        {
            return records; // 빈 결과 반환
        }
        string[] paths = Directory.GetFiles(DirectoryPath, "trial_*.json"); // 실제 시험 결과만 조회
        Array.Sort(paths, StringComparer.Ordinal); // 날짜 순서가 가능한 파일 이름 정렬
        for (int i = paths.Length - 1; i >= 0 && records.Count < Math.Max(1, limit); i--) // 최근 기록만 제한해 읽기
        {
            try // 손상된 한 파일과 다른 결과 분리
            {
                if (new FileInfo(paths[i]).Length > 4 * 1024 * 1024) // 비정상적으로 큰 파일 제외
                {
                    continue; // 다음 결과 조회
                }
                BalanceTrialRecord record = JsonUtility.FromJson<BalanceTrialRecord>(File.ReadAllText(paths[i])); // 저장 결과 읽기
                if (record != null && record.schema == 1 && !string.IsNullOrEmpty(record.id)) // 지원하는 정상 형식 확인
                {
                    records.Add(record); // 비교 가능한 결과 보관
                }
            }
            catch (Exception error) // 손상된 파일 안내
            {
                Debug.LogWarning("시험 결과 읽기 실패: " + paths[i] + " / " + error.Message); // 무시한 파일 표시
            }
        }
        return records; // 기존 기록 반환
    }

    public static string NewId() // 시간순 정렬과 충돌 방지
    {
        return DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N"); // 시각과 무작위 번호 조합
    }
}
