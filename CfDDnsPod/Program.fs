open System
open FsToolkit.ErrorHandling
open FSharp.Data
open RestSharp
open CfDDnsPod.DnsPodApi

let SECRET_ID = Environment.GetEnvironmentVariable("ddns_secret_id")
let SECRET_KEY = Environment.GetEnvironmentVariable("ddns_secret_key")
let DOMAIN = Environment.GetEnvironmentVariable("ddns_domain")
let SUBDOMAIN = Environment.GetEnvironmentVariable("ddns_subdomain")
let INTERVAL = Environment.GetEnvironmentVariable("ddns_interval") |> int

type RecordList =
    JsonProvider<
        Sample="""{"Response": {"RecordList": [{"RecordId": "typeof<int64>", "Value": "typeof<string>"}]}}""",
        InferenceMode=InferenceMode.ValuesAndInlineSchemasOverrides
     >

let cred =
    {| SecretId = SECRET_ID
       SecretKey = SECRET_KEY |}

let client =
    new RestClient(RestClientOptions("https://dnspod.tencentcloudapi.com", Timeout = Nullable(TimeSpan.FromSeconds(5))))

let service = "dnspod"
let version = "2021-03-23"

let getRecord () = async {
    let domain = DOMAIN
    let subdomain = SUBDOMAIN

    let req = $$"""{"Domain":"{{domain}}","Subdomain":"{{subdomain}}"}"""
    let! resp = doRequest client cred.SecretId cred.SecretKey version "DescribeRecordList" req "" ""

    try
        let ret = RecordList.Parse(resp)

        if ret.Response.RecordList = null || ret.Response.RecordList.Length = 0 then
            return None
        else
            return Some((ret.Response.RecordList[0].RecordId, ret.Response.RecordList[0].Value))
    with e ->
        eprintfn $"%s{e.Message}: {resp}"
        return None
}

let modifyRecord (recordId: int64) (ip: string) = async {
    let req =
        $$"""{"Domain":"{{DOMAIN}}","RecordType":"A","RecordLine":"默认","Value":"{{ip}}","RecordId":{{recordId}},"SubDomain":"{{SUBDOMAIN}}","TTL":601}"""

    printfn $"{req}"

    let! resp = doRequest client cred.SecretId cred.SecretKey version "ModifyRecord" req "" ""
    printfn $"modifyRecord: {resp}"
    ()
}

let getIp () = async {
    let options = RestClientOptions("https://cloudflare.com")
    use client = new RestClient(options)
    let req = RestRequest("/cdn-cgi/trace")
    let! ret = client.ExecuteAsync(req) |> Async.AwaitTask

    let ip =
        ret.Content.Trim().Split()
        |> Seq.map _.Split('=')
        |> Seq.filter (fun x -> x.Length = 2 && x[0] = "ip")
        |> Seq.map (fun x -> x[1])
        |> Seq.toArray
        |> Array.tryHead

    return ip
}

let run () = asyncResult {
    let! ipNew = getIp () |> AsyncResult.requireSome "ip not found"
    let! recordId, ipOld = getRecord () |> AsyncResult.requireSome "record not found"

    if ipNew <> ipOld then
        printfn $"change from {ipOld} to {ipNew}"
        do! modifyRecord recordId ipNew
        return true
    else
        printfn "no change"
        return false
}

let loop () = async {
    while true do
        let! _ = run ()
        do! Async.Sleep (INTERVAL*1000)
}

loop () |> Async.RunSynchronously
