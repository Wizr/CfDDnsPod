open System
open FsToolkit.ErrorHandling
open FSharp.Data
open RestSharp

open CfDDnsPod.DnsPodTypes
open CfDDnsPod.DnsPodApi

module ParseEnv =
    let SECRET_ID = Environment.GetEnvironmentVariable("ddns_secret_id")
    let SECRET_KEY = Environment.GetEnvironmentVariable("ddns_secret_key")
    let DOMAIN = Environment.GetEnvironmentVariable("ddns_domain")
    let SUBDOMAIN = Environment.GetEnvironmentVariable("ddns_subdomain")
    let INTERVAL = Environment.GetEnvironmentVariable("ddns_interval") |> int

module Parser =
    type RecordsParser =
        JsonProvider<
            Sample="""[
                {"Response": {"RecordList": [{"RecordId": "typeof<int64>", "Value": "typeof<string>"}]}},
                {"Response": {"Error": {"Code": "typeof<string>", "Message": "typeof<string>"}}}
            ]""",
            SampleIsList=true,
            InferenceMode=InferenceMode.ValuesAndInlineSchemasOverrides
         >

    type IpResponseParser =
        JsonProvider<
            Sample="""{"query": "typeof<string>"}""",
            InferenceMode=InferenceMode.ValuesAndInlineSchemasOverrides
         >

let CreateDnsPodRequest (data: DnsPodActionKinds) =
    DnsPodAction.New
        {
            SecretId = ParseEnv.SECRET_ID
            SecretKey = ParseEnv.SECRET_KEY
        }
        data

let client =
    new RestClient(RestClientOptions("https://dnspod.tencentcloudapi.com", Timeout = TimeSpan.FromSeconds 5L))

let rec getRecord () = asyncResult {
    let req =
        $$"""{"Domain":"{{ParseEnv.DOMAIN}}","Subdomain":"{{ParseEnv.SUBDOMAIN}}"}"""

    let! resp = doRequest client (CreateDnsPodRequest(DescribeRecordList req))

    try
        let ret = Parser.RecordsParser.Parse(resp)

        if ret.Response.Error.IsSome then
            return! AsyncResult.error $"{nameof getRecord}: {ret.Response.Error.Value.Message}"

        if ret.Response.RecordList = null || ret.Response.RecordList.Length = 0 then
            return! AsyncResult.error $"{nameof getRecord}: empty RecordList"
        else
            return ret.Response.RecordList[0].RecordId, ret.Response.RecordList[0].Value
    with e ->
        return! AsyncResult.error $"{nameof getRecord}: exception: {e.Message}; resp: {resp}"
}

let rec modifyRecord (recordId: int64) (ip: string) = async {
    let req =
        $$"""{"Domain":"{{ParseEnv.DOMAIN}}","RecordType":"A","RecordLine":"默认","Value":"{{ip}}","RecordId":{{recordId}},"SubDomain":"{{ParseEnv.SUBDOMAIN}}","TTL":601}"""

    printfn $"{nameof getRecord}.req: {req}"
    let! resp = doRequest client (CreateDnsPodRequest(ModifyRecord req))
    printfn $"{nameof modifyRecord}.resp: {resp}"
    ()
}

let rec getIp () = asyncResult {
    let options = RestClientOptions("http://ip-api.com/json")
    use client = new RestClient(options)
    let req = RestRequest()
    let! ret = client.ExecuteAsync(req) |> Async.AwaitTask

    try
        return Parser.IpResponseParser.Parse(ret.Content).Query
    with e ->
        return! AsyncResult.error $"{nameof getIp}: {e.Message}"
}

let run () = asyncResult {
    let! ipNew = getIp ()
    let! recordId, ipOld = getRecord ()

    if ipNew <> ipOld then
        printfn $"change from {ipOld} to {ipNew}"
        do! modifyRecord recordId ipNew
        return true
    else
        printfn $"keep current: {ipOld}"
        return false
}

let loop () = async {
    printfn $"loop per {ParseEnv.INTERVAL}s for domain {ParseEnv.SUBDOMAIN}.{ParseEnv.DOMAIN}"

    while true do
        let! ret = run ()

        match ret with
        | Error(e) -> printfn $"{e}"
        | _ -> ()

        do! Async.Sleep(ParseEnv.INTERVAL * 1000)
}

loop () |> Async.RunSynchronously
