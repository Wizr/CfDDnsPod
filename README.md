一个简单的 ddns 小工具，单文件、无依赖、易部署。

## 使用

| 环境变量 | 说明 |
| --- | :--- |
| ddns_secret_id | 申请安全凭证的具体步骤如下：<br/><br/>1. 登录 腾讯云管理中心控制台 。<br/>2. 前往 云API密钥 的控制台页面。<br/>3. 在 [云 API 密钥](https://console.cloud.tencent.com/capi) 页面，单击【新建密钥】创建一对密钥。|
| ddns_secret_key | 同上 |
| ddns_domain | 域名，例如: example.com |
| ddns_subdomain | 二级域名，例如: www 或 demo |
| ddns_interval | 检查 IP 变化的时间间隔，单位秒 |

示例
```bash
ddns_secret_id=<id> \
ddns_secret_key=<key> \
ddns_domain=example.com \
ddns_subdomain=demo \
ddns_interval=10 \
./CfDDnsPod
```
