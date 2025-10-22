import os
import requests
from dotenv import load_dotenv

load_dotenv()
TOKEN = os.getenv("GH_RELEASE_TOKEN")
REPO_OWNER = "Creeper-of-Fire"
REPO_NAME = "YAESandBox"
API_URL = f"https://api.github.com/repos/{REPO_OWNER}/{REPO_NAME}/releases"

headers = {
    "Authorization": f"token {TOKEN}",
    "Accept": "application/vnd.github.v3+json"
}

try:
    response = requests.get(API_URL, headers=headers)
    response.raise_for_status()
    releases = response.json()

    with open("AllReleases_from_api.md", "w", encoding="utf-8") as f:
        for release in releases:
            tag_name = release.get("tag_name", "N/A")
            body = release.get("body", "No description provided.")
            f.write(f"---\n## Release: {tag_name}\n---\n")
            f.write(body)
            f.write("\n\n")

    print("✅ 所有发布说明已从 API 导出到 AllReleases_from_api.md 文件！")

except requests.exceptions.RequestException as e:
    print(f"❌ 请求失败: {e}")