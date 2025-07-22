-- 该脚本用于内省 Lua 的标准库，并生成一个 JSON 骨架文件。
-- 这个骨架文件后续可以手动填充详细的文档和签名。

local json = require "dkjson" -- 你可能需要安装一个 json 库, 例如 lua-cjson 或 dkjson

-- 要导出的标准库列表
local libs = {
    -- _G 包含 print, ipairs, pairs, type 等全局函数
    { name = "_G", lib = _G },
    { name = "string", lib = string },
    { name = "table", lib = table },
    { name = "math", lib = math },
    { name = "coroutine", lib = coroutine },
    { name = "os", lib = os },
    { name = "io", lib = io },
    { name = "debug", lib = debug },
    { name = "package", lib = package },
    { name = "utf8", lib = utf8 } -- Lua 5.3+
}

local api_data = {}

print("-- 开始生成 Lua 标准库 API 骨架...")

for _, data in ipairs(libs) do
    local lib_name = data.name
    local lib_table = data.lib
    
    local api_object = {
        documentation = "Lua 标准库 " .. lib_name,
        methods = {}
    }

    -- 使用 pairs 遍历库中的所有键值对
    for key, value in pairs(lib_table) do
        -- 我们只关心函数，并且排除私有成员 (以 _ 开头)
        if type(value) == "function" and not key:match("^_") then
            -- 为每个函数创建一个占位符条目
            local method_entry = {
                name = key,
                signature = lib_name .. "." .. key .. "(...)", -- 占位符签名
                documentation = "TODO: 从官方文档添加 " .. lib_name .. "." .. key .. " 的说明。",
                insertText = key .. "(${1})" -- 基础的代码片段
            }
            table.insert(api_object.methods, method_entry)
        end
    end

    -- 将方法按字母顺序排序，方便查找
    table.sort(api_object.methods, function(a, b) return a.name < b.name end)
    
    -- 将处理好的库数据存入最终结果
    -- 对于 _G，我们特殊处理，因为它的函数是全局的，没有前缀
    if lib_name == "_G" then
        api_data["global"] = api_object
        api_data["global"].documentation = "Lua 全局函数"
    else
        api_data[lib_name] = api_object
    end
end

print("-- 生成完成。请将以下 JSON 内容保存到 lua-stdlib-api.json 文件中。")

-- 将 Lua table 编码为格式化的 JSON 并打印到控制台
-- 如果你使用的 json 库不支持 pretty print，输出可能是一整行
print(json.encode(api_data, {indent=true}, "  "))