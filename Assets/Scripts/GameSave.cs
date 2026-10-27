using System;
using UnityEngine;

namespace ElementalBreaker
{
    // 판을 저장한 순간이야. 이어하기는 이 순간부터 다시 시작해.
    public enum SavePhase
    {
        // 새 줄이 막 생긴 뒤야. 이번 턴 이벤트(해금, 골드, 디버프 배너)는 아직 안 봤어.
        TurnStart,
        // 조준 중이야. 이벤트를 다 봤고, 상점·공 속성·얼음을 쓰면 다시 저장해.
        Aiming,
        // 공을 쐈어. 이어하면 그 샷은 아무것도 못 맞힌 걸로 치고 바로 턴을 끝내.
        ShotFired
    }

    [Serializable]
    public struct SavedBrick
    {
        public int row;
        public int column;
        public int element;
        public int health;
        public bool boosted;
    }

    [Serializable]
    public struct SavedPickup
    {
        public int row;
        public int column;
        public int element;
    }

    // 이어하기에 필요한 판 전체야. 해금 상태와 상점 가격은 턴 수로 다시 계산돼서 넣지 않아.
    [Serializable]
    public sealed class SaveData
    {
        public int version;
        public int phase;
        public int turn;
        public int gold;
        // 가진 공 수야. 공이 날아가는 중에 얻은 공도 들어 있어.
        public int balls;
        public int turnElement;
        // 다음 턴에 쓸 공 속성(공 아이템을 먹었을 때)이야. 없으면 -1이야.
        public int reservedElement = -1;
        public float launchX;
        public int heldItem;
        public int iceUseCount;
        public bool frozen;
        // 체력 디버프가 아직 새 줄에 안 들어갔어(얼음 턴이라 줄이 안 생겼을 때).
        public bool pendingHealthBoost;
        public bool brickChangeUsed;
        // 32턴부터 큰 디버프를 무작위로 고르는 씨앗값이야. 이어해도 같은 디버프가 나와.
        public int debuffSeed;
        public SavedBrick[] bricks = Array.Empty<SavedBrick>();
        public SavedPickup[] pickups = Array.Empty<SavedPickup>();
    }

    // 판 저장소야. 되돌릴 수 없는 행동(발사, 아이템, 구매, 새 줄)을 하는 순간 바로 저장해서
    // 나갔다 들어와도 결과를 바꿀 수 없어. 저장할 때마다 직전 저장을 백업으로 하나 남겨.
    // 읽을 때는 끝까지 읽고 검사한 뒤에만 써. 이상하면 백업을 쓰고, 그것도 이상하면 저장이 없는 걸로 쳐.
    public static class GameSave
    {
        // 저장 형식을 바꾸면 이 숫자를 올려. 예전 형식의 저장은 버려.
        public const int Version = 1;
        private const string Key = "ElementalBreaker.Save";
        private const string BackupKey = "ElementalBreaker.Save.Backup";
        private const int ElementCount = 4;
        private const int MaxTurn = 100000;
        private const int MaxBalls = 100000;
        // 얼음 가격이 2배씩 오르는데, 이보다 크면 가격 계산이 넘쳐.
        private const int MaxIceUses = 30;

        // 로비에서 이어하기를 누르면 여기에 담아. 게임 씬의 보드가 꺼내 쓰고 비워.
        public static SaveData Pending { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Pending = null;

        public static void Write(SaveData data)
        {
            if (data == null)
                return;
            data.version = Version;
            string json;
            try
            {
                json = JsonUtility.ToJson(data);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return;
            }
            string previous = PlayerPrefs.GetString(Key, string.Empty);
            if (!string.IsNullOrEmpty(previous))
                PlayerPrefs.SetString(BackupKey, previous);
            PlayerPrefs.SetString(Key, json);
            // 웹에서는 탭을 닫을 때 저장할 기회가 없어서, 쓸 때마다 바로 확정해.
            PlayerPrefs.Save();
        }

        public static bool TryLoad(out SaveData data)
        {
            if (TryRead(Key, out data))
                return true;
            if (TryRead(BackupKey, out data))
            {
                Debug.LogWarning("저장이 망가져서 직전 저장으로 이어해.");
                return true;
            }
            return false;
        }

        public static void Delete()
        {
            Pending = null;
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.DeleteKey(BackupKey);
            PlayerPrefs.Save();
        }

        // 저장했다가 다시 읽어도 같은지 확인해. 빠진 항목이나 검사에 걸리는 값을 찾을 때 써.
        public static bool RoundTrips(SaveData data, out string json)
        {
            json = JsonUtility.ToJson(data);
            SaveData copy = Parse(json);
            return copy != null && IsValid(copy) && JsonUtility.ToJson(copy) == json;
        }

        private static bool TryRead(string key, out SaveData data)
        {
            data = Parse(PlayerPrefs.GetString(key, string.Empty));
            if (data != null && IsValid(data))
                return true;
            data = null;
            return false;
        }

        private static SaveData Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
                return null;
            try
            {
                return JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception)
            {
                return null;
            }
        }

        // 게임판 크기와 상관없이 확인할 수 있는 값이야.
        public static bool IsValid(SaveData data)
        {
            if (data == null || data.version != Version || data.phase < 0 || data.phase > (int)SavePhase.ShotFired ||
                data.turn < 1 || data.turn > MaxTurn || data.gold < 0 || data.balls < 1 || data.balls > MaxBalls ||
                !IsElement(data.turnElement) || (data.reservedElement != -1 && !IsElement(data.reservedElement)) ||
                data.heldItem < (int)ItemType.None || data.heldItem > (int)ItemType.Ice ||
                data.iceUseCount < 0 || data.iceUseCount > MaxIceUses ||
                float.IsNaN(data.launchX) || float.IsInfinity(data.launchX))
                return false;
            // 새 줄이 막 생긴 순간에는 얼음이 아직 안 쓰였어.
            if (data.phase == (int)SavePhase.TurnStart && data.frozen)
                return false;
            if (data.bricks != null)
                foreach (SavedBrick brick in data.bricks)
                    if (!IsElement(brick.element) || brick.health < 1)
                        return false;
            if (data.pickups != null)
                foreach (SavedPickup pickup in data.pickups)
                    if (!IsElement(pickup.element))
                        return false;
            return true;
        }

        // 지금 게임판에 그대로 놓을 수 있는지야. 벽돌이 위험 행에 있으면 이미 진 판이라 쓰지 않아.
        public static bool FitsBoard(SaveData data, int rows, int columns, int dangerRow)
        {
            if (!IsValid(data))
                return false;
            bool[,] taken = new bool[rows, columns];
            if (data.bricks != null)
                foreach (SavedBrick brick in data.bricks)
                    if (!TryTake(taken, brick.row, brick.column, dangerRow, columns))
                        return false;
            if (data.pickups != null)
                foreach (SavedPickup pickup in data.pickups)
                    if (!TryTake(taken, pickup.row, pickup.column, dangerRow, columns))
                        return false;
            return true;
        }

        private static bool TryTake(bool[,] taken, int row, int column, int dangerRow, int columns)
        {
            if (row < 0 || row >= dangerRow || column < 0 || column >= columns || taken[row, column])
                return false;
            taken[row, column] = true;
            return true;
        }

        private static bool IsElement(int value) => value >= 0 && value < ElementCount;
    }
}
