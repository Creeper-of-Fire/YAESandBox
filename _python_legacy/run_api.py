# run_api.py
import uvicorn
import os

if __name__ == "__main__":
    # 获取主机和端口，提供默认值
    host = os.getenv("API_HOST", "127.0.0.1")
    port = int(os.getenv("API_PORT", "6700"))
    reload = os.getenv("API_RELOAD", "true").lower() == "true" # 开发时启用自动重载

    print(f"启动 API 服务器于 http://{host}:{port}")
    print(f"自动重载: {'启用' if reload else '禁用'}")

    uvicorn.run(
        "api.main:app", # 指向 FastAPI 应用实例
        host=host,
        port=port,
        reload=reload # 启用自动重载，方便开发
    )