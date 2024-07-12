module CfDDnsPod.DnsPodApi

open System
open System.Text
open System.Security.Cryptography

open RestSharp

module Utils =
    let sha256Hex (s: string) =
        use algo = SHA256.Create()
        let hashBytes = algo.ComputeHash(Encoding.UTF8.GetBytes(s))
        hashBytes |> Seq.map (fun b -> $"%02x{b}") |> String.concat ""

    let hmacSha256 (key: byte[]) (msg: byte[]) =
        use mac = new HMACSHA256(key)
        mac.ComputeHash(msg)


let getAuth
    (secretId: string)
    (secretKey: string)
    (host: string)
    (contentType: string)
    (action: string)
    (timestamp: string)
    (body: string)
    =
    let canonicalInfo =
        let signedHeaders = "content-type;host;x-tc-action"

        let canonicalHeaders =
            $"content-type:{contentType}\nhost:{host}\nx-tc-action:{action.ToLower()}"

        let canonicalRequest =
            let canonicalURI = "/"
            let hashedRequestPayload = Utils.sha256Hex body
            $"POST\n{canonicalURI}\n\n{canonicalHeaders}\n\n{signedHeaders}\n{hashedRequestPayload}"

        {| canonicalRequest = canonicalRequest
           signedHeaders = signedHeaders |}

    let algorithm = "TC3-HMAC-SHA256"

    let date =
        DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddSeconds(float timestamp)
            .ToString("yyyy-MM-dd")

    let service = host.Split(".")[0]
    let credentialScope = $"{date}/{service}/tc3_request"

    let signature =
        let stringToSign =
            let hashedCanonicalRequest = Utils.sha256Hex canonicalInfo.canonicalRequest
            $"{algorithm}\n{timestamp}\n{credentialScope}\n{hashedCanonicalRequest}"

        let signatureBytes =
            [| $"TC3{secretKey}"; date; service; "tc3_request"; stringToSign |]
            |> Array.map Encoding.UTF8.GetBytes
            |> Array.reduce Utils.hmacSha256

        BitConverter.ToString(signatureBytes).Replace("-", "").ToLower()

    $"{algorithm} Credential={secretId}/{credentialScope}, SignedHeaders={canonicalInfo.signedHeaders}, Signature={signature}"

let buildRequest
    (secretId: string)
    (secretKey: string)
    (version: string)
    (action: string)
    (body: string)
    (region: string)
    (token: string)
    =
    let host = "dnspod.tencentcloudapi.com"
    let contentType = "application/json; charset=utf-8"

    let timestamp =
        DateTime.UtcNow.Subtract(DateTime(1970, 1, 1)).TotalSeconds |> int |> string

    let request = RestRequest("/", Method.Post, Timeout = TimeSpan.FromSeconds(5))

    let auth = getAuth secretId secretKey host contentType action timestamp body

    request
        .AddHeader("Host", host)
        .AddHeader("X-TC-Timestamp", timestamp)
        .AddHeader("X-TC-Version", version)
        .AddHeader("X-TC-Action", action)
        .AddHeader("X-TC-Region", region)
        .AddHeader("X-TC-Token", token)
        .AddHeader("X-TC-RequestClient", "SDK_NET_BAREBONE")
        .AddHeader("Authorization", auth)
        .AddStringBody(body, ContentType.Json)
    |> ignore

    request


let doRequest
    (client: RestClient)
    (secretId: string)
    (secretKey: string)
    (version: string)
    (action: string)
    (body: string)
    (region: string)
    (token: string)
    =
    async {
        let request = buildRequest secretId secretKey version action body region token

        let! response = client.PostAsync request |> Async.AwaitTask
        return response.Content
    }
