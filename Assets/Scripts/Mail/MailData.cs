using BackEnd;
using LitJson;
using Muks.BackEnd;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

/// <summary>우편 첨부 아이템 한 건 (GetPostList 응답의 items 배열 요소)</summary>
public class MailItem
{
    public string ItemName { get; set; }
    public int ItemCount { get; set; }
}

/// <summary>뒤끝 서버에서 가져온 우편 한 건의 데이터 (Backend.UPost 기준)</summary>
public class MailData
{
    public PostType PostType { get; private set; }
    public string InDate { get; private set; }
    public string Title { get; private set; }
    public string Content { get; private set; }
    public string Author { get; private set; }
    public DateTime ExpirationDate { get; private set; }
    public bool IsReceived { get; private set; }
    public bool IsHidden { get; private set; }
    public List<MailItem> Items { get; private set; }
    public bool HasValidRewardManifest { get; private set; }
    public string RewardManifestError { get; private set; }
    public IReadOnlyList<MailRewardItem> RewardManifest { get; private set; }
    public string RawRewardManifestJson { get; private set; }
    public GameDataRestoreQuery SourceQuery { get; private set; }

    /// <summary>GameData 수령 이력 레코드의 inDate (이력 메일만 설정됨)</summary>
    public string HistoryInDate { get; private set; }
    /// <summary>GameData 수령 이력에서 로드된 메일이면 true</summary>
    public bool IsFromHistory { get; private set; }

    /// <summary>UI에서 숨겨야 하면 true (도착일+30일 초과 또는 만료일 초과 중 이른 것)</summary>
    public bool IsExpired
    {
        get
        {
            DateTime now = UserInfo.GetKoreanTime();
            if (now > ExpirationDate) return true;
            if (DateTime.TryParse(InDate, out DateTime arrivedDate) && now > arrivedDate.AddDays(30)) return true;
            return false;
        }
    }

    /// <summary>서버에서 삭제해야 하면 true (만료일 초과)</summary>
    public bool IsServerExpired => UserInfo.GetKoreanTime() > ExpirationDate;

    // UPost 응답으로 생성
    public MailData(PostType postType, JsonData json, GameDataRestoreQuery sourceQuery = null)
    {
        PostType = postType;
        SourceQuery = sourceQuery;
        Items = new List<MailItem>();
        RewardManifest = new ReadOnlyCollection<MailRewardItem>(new List<MailRewardItem>());

        try
        {
            InDate  = json.ContainsKey("inDate")   ? json["inDate"].ToString()   : string.Empty;
            Title   = json.ContainsKey("title")    ? json["title"].ToString()    : string.Empty;
            Content = json.ContainsKey("content")  ? json["content"].ToString()  : string.Empty;
            Author  = json.ContainsKey("author")   ? json["author"].ToString()   : string.Empty;

            // 서버에서 이미 수령된 메일이면 수령 완료로 표시
            if (json.ContainsKey("isRead"))
                IsReceived = json["isRead"].ToString() != "0";
            else
                IsReceived = false;

            if (json.ContainsKey("expirationDate") &&
                DateTime.TryParse(json["expirationDate"].ToString(), out DateTime expDate))
                ExpirationDate = expDate;
            else
                ExpirationDate = DateTime.MaxValue;

            // Keep the complete original manifest separate from mutable presentation items.
            RawRewardManifestJson = json.ContainsKey("items") && json["items"] != null ? json["items"].ToJson() : null;
            HasValidRewardManifest = MailRewardParser.TryParse(RawRewardManifestJson,
                out IReadOnlyList<MailRewardItem> rewards, out string manifestError);
            if (HasValidRewardManifest)
            {
                RewardManifest = rewards;
                foreach (MailRewardItem item in rewards)
                    Items.Add(new MailItem { ItemName = item.Id, ItemCount = item.Count });
            }
            else RewardManifestError = manifestError;
        }
        catch (Exception ex)
        {
            HasValidRewardManifest = false;
            RewardManifestError = ex.GetType().Name;
            Debug.LogError($"[MailData] JSON 파싱 오류: {ex.Message}");
        }
    }

    // 이력 레코드로 생성 (기본 생성자)
    private MailData() { }

    public void SetReceived() => IsReceived = true;
    public void SetHidden() => IsHidden = true;

    public void SetHistoryInDate(string histInDate)
    {
        HistoryInDate = histInDate;
        IsFromHistory = !string.IsNullOrEmpty(histInDate);
    }

    /// <summary>UPost 메일에 이력 레코드 inDate만 연결합니다. IsFromHistory는 변경하지 않습니다.</summary>
    public void LinkHistoryRecord(string histInDate) => HistoryInDate = histInDate;

    /// <summary>GameData에 저장할 Param 직렬화</summary>
    public Param ToHistoryParam()
    {
        Param p = new Param();
        p.Add("originalInDate", InDate);
        p.Add("postType", PostType.ToString());
        p.Add("title", Title ?? string.Empty);
        p.Add("content", Content ?? string.Empty);
        p.Add("author", Author ?? string.Empty);
        p.Add("expirationDate", ExpirationDate == DateTime.MaxValue
            ? string.Empty : ExpirationDate.ToString("O"));

        // Items → JSON 문자열
        var sb = new System.Text.StringBuilder("[");
        for (int i = 0; i < Items.Count; i++)
        {
            if (i > 0) sb.Append(",");
            sb.Append($"{{\"name\":\"{Items[i].ItemName}\",\"count\":{Items[i].ItemCount}}}");
        }
        sb.Append("]");
        p.Add("items", sb.ToString());
        p.Add("isReceived", IsReceived ? 1 : 0);
        p.Add("receivedAt", IsReceived ? UserInfo.GetKoreanTime().ToString("O") : string.Empty);
        p.Add("isHidden", IsHidden ? 1 : 0);

        return p;
    }

    /// <summary>GameData 이력 행에서 MailData 복원</summary>
    public static MailData CreateFromHistory(string historyRecordInDate, JsonData row, GameDataRestoreQuery sourceQuery = null)
    {
        var data = new MailData();
        data.SourceQuery = sourceQuery;
        data.RewardManifest = new ReadOnlyCollection<MailRewardItem>(new List<MailRewardItem>());
        data.Items       = new List<MailItem>();
        data.IsReceived  = !row.ContainsKey("isReceived") || row["isReceived"].ToString() != "0";
        data.IsHidden    = row.ContainsKey("isHidden") && row["isHidden"].ToString() == "1";
        data.IsFromHistory = true;
        data.HistoryInDate = historyRecordInDate;

        try
        {
            data.InDate   = row.ContainsKey("originalInDate") ? row["originalInDate"].ToString() : historyRecordInDate;
            data.PostType = row.ContainsKey("postType") && row["postType"].ToString() == "Coupon"
                            ? PostType.Coupon : PostType.Admin;
            data.Title    = row.ContainsKey("title")   ? row["title"].ToString()   : string.Empty;
            data.Content  = row.ContainsKey("content") ? row["content"].ToString() : string.Empty;
            data.Author   = row.ContainsKey("author")  ? row["author"].ToString()  : string.Empty;

            string expStr = row.ContainsKey("expirationDate") ? row["expirationDate"].ToString() : string.Empty;
            data.ExpirationDate = !string.IsNullOrEmpty(expStr) &&
                                  DateTime.TryParse(expStr, out DateTime exp)
                                  ? exp : DateTime.MaxValue;

            // items JSON 문자열 파싱
            if (row.ContainsKey("items"))
            {
                string itemsStr = row["items"].ToString();
                if (!string.IsNullOrEmpty(itemsStr) && itemsStr != "[]")
                {
                    JsonData arr = JsonMapper.ToObject(itemsStr);
                    for (int i = 0; i < arr.Count; i++)
                    {
                        var mi = new MailItem();
                        if (arr[i].ContainsKey("name"))  mi.ItemName  = arr[i]["name"].ToString();
                        if (arr[i].ContainsKey("count") &&
                            int.TryParse(arr[i]["count"].ToString(), out int cnt))
                            mi.ItemCount = cnt;
                        if (!string.IsNullOrEmpty(mi.ItemName)) data.Items.Add(mi);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MailData] 이력 파싱 오류: {ex.Message}");
        }

        return data;
    }
}
