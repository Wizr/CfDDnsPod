module CfDDnsPod.DnsPodApi

open System
open System.Text
open System.Security.Cryptography
open RestSharp

open CfDDnsPod.DnsPodTypes

module Utils =
    let sha256Hex (s: string) =
        use algo = SHA256.Create()
        let hashBytes = algo.ComputeHash(Encoding.UTF8.GetBytes(s))
        hashBytes |> Seq.map (fun b -> $"%02x{b}") |> String.concat ""

    let hmacSha256 (key: byte[]) (msg: byte[]) =
        use mac = new HMACSHA256(key)
        mac.ComputeHash(msg)

module Authorization =
    let contentType = "application/json; charset=utf-8"
    let signedHeaders = "content-type;host;x-tc-action"
    let algorithm = "TC3-HMAC-SHA256"

    type DnsPodAction with
        member this.timestampStr =
            DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                .AddSeconds(float this.Timestamp)
                .ToString("yyyy-MM-dd")

        member this.canonicalHeaders =
            $"content-type:{contentType}\nhost:{this.Host}\nx-tc-action:{this.Kind.Name.ToLower()}"

        member this.canonicalRequest =
            let canonicalURI = "/"
            let hashedRequestPayload = Utils.sha256Hex this.Kind.Value
            $"POST\n{canonicalURI}\n\n{this.canonicalHeaders}\n\n{signedHeaders}\n{hashedRequestPayload}"

        member this.service = this.Host.Split(".")[0]
        member this.credentialScope = $"{this.timestampStr}/{this.service}/tc3_request"

        member this.signature =
            let stringToSign =
                let hashedCanonicalRequest = Utils.sha256Hex this.canonicalRequest
                $"{algorithm}\n{this.Timestamp}\n{this.credentialScope}\n{hashedCanonicalRequest}"

            let signatureBytes =
                [|
                    $"TC3{this.Cred.SecretKey}"
                    this.timestampStr
                    this.service
                    "tc3_request"
                    stringToSign
                |]
                |> Array.map Encoding.UTF8.GetBytes
                |> Array.reduce Utils.hmacSha256

            BitConverter.ToString(signatureBytes).Replace("-", "").ToLower()

    let GetString action =
        $"{algorithm} Credential={action.Cred.SecretId}/{action.credentialScope}, SignedHeaders={signedHeaders}, Signature={action.signature}"

type DnsPodAction with
    member this.ToRestSharpRequest() =
        let timestamp = this.Timestamp |> string
        let request = RestRequest("/", Method.Post, Timeout = TimeSpan.FromSeconds 5L)
        let auth = Authorization.GetString this

        request
            .AddHeader("Host", this.Host)
            .AddHeader("X-TC-Timestamp", timestamp)
            .AddHeader("X-TC-Version", this.Version)
            .AddHeader("X-TC-Action", this.Kind.Name)
            .AddHeader("X-TC-Region", this.Region)
            .AddHeader("X-TC-Token", this.Token)
            .AddHeader("X-TC-RequestClient", "SDK_NET_BAREBONE")
            .AddHeader("Authorization", auth)
            .AddStringBody(this.Kind.Value, ContentType.Json)
        |> ignore

        request


let doRequest (client: RestClient) (action: DnsPodAction) = async {
    let request = action.ToRestSharpRequest()
    let! response = client.PostAsync request |> Async.AwaitTask
    return response.Content
}
