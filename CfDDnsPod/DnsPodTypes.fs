module CfDDnsPod.DnsPodTypes

open System


type DnsPodCredential = { SecretId: string; SecretKey: string }

type DnsPodActionKinds =
    | DescribeRecordList of string
    | ModifyRecord of string

    member this.Name =
        match this with
        | DescribeRecordList _ -> nameof DescribeRecordList
        | ModifyRecord _ -> nameof ModifyRecord

    member this.Value =
        match this with
        | DescribeRecordList v -> v
        | ModifyRecord v -> v

type DnsPodAction = {
    Host: string
    Cred: DnsPodCredential
    Version: string
    Region: string
    Token: string
    Kind: DnsPodActionKinds
    Timestamp: int
} with

    static member New (cred: DnsPodCredential) (kind: DnsPodActionKinds) = {
        Host = "dnspod.tencentcloudapi.com"
        Cred = cred
        Version = "2021-03-23"
        Region = ""
        Token = ""
        Kind = kind
        Timestamp = DateTime.UtcNow.Subtract(DateTime(1970, 1, 1)).TotalSeconds |> int
    }
