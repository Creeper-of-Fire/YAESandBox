import tkinter as tk
from tkinter import filedialog, messagebox
import json
import os
import re # 导入正则表达式模块
import uuid

# --- 正则表达式定义 ---
# 使用 re.DOTALL 标志使 . 匹配换行符
SETVAR_REGEX = re.compile(r"\{\{setvar::(.*?)::(.*?)\}\}", re.DOTALL)
GETVAR_REGEX = re.compile(r"\{\{getvar::(.*?)\}\}", re.DOTALL)


# --- 核心转换逻辑 ---
def convert_prompt_format(source_prompt: dict) -> list[dict]:
    generated_runes = []
    content = source_prompt.get("content", "")

    # 1. 处理 {{setvar::...}} 语法 (现在支持多行)
    setvar_matches = list(SETVAR_REGEX.finditer(content))
    if setvar_matches:
        script_lines = []
        for match in setvar_matches:
            var_name = match.group(1).strip()
            var_value = match.group(2).strip()

            # 智能判断使用何种引号
            if '\n' in var_value:
                # 如果值包含换行符，使用三引号
                script_lines.append(f'{var_name} = """{var_value}"""')
            else:
                # 否则，使用常规双引号，并对内部的双引号进行转义
                escaped_value = var_value.replace('"', '\\"')
                script_lines.append(f'{var_name} = "{escaped_value}"')

        static_vars_rune = {
            "runeType": "StaticVariableRuneConfig",
            "name": f"变量定义 (来自: {source_prompt.get('name', '未命名')})",
            "enabled": source_prompt.get("enabled", True),
            "configId": f"{source_prompt.get('identifier', uuid.uuid4())}_vars",
            "scriptContent": "\n".join(script_lines)
        }
        generated_runes.append(static_vars_rune)
        content = SETVAR_REGEX.sub("", content).strip()

    # 2. 处理 {{getvar::...}} 语法 (现在支持多行)
    final_template = GETVAR_REGEX.sub(r"[[\1]]", content)

    # 3. 创建 PromptGenerationRuneConfig
    pos_map = {0: "After", 1: "Before"}
    insertion_position = pos_map.get(source_prompt.get("injection_position"), "After")
    role_type = source_prompt.get('role', 'user').capitalize()

    prompt_gen_rune = {
        "runeType": "PromptGenerationRuneConfig",
        "name": source_prompt.get("name", "未命名提示词"),
        "enabled": source_prompt.get("enabled", True),
        "configId": source_prompt.get("identifier"),
        "promptsName": "Prompts",
        "isAppendMode": False,
        "template": final_template,
        "insertionDepth": source_prompt.get("injection_depth", 0),
        "roleType": role_type,
        "insertionPosition": insertion_position,
    }
    generated_runes.append(prompt_gen_rune)

    return generated_runes


# --- GUI 应用部分 (仅修改了 convert_and_save 方法) ---
class PromptConverterApp:
    def __init__(self, master):
        self.master = master
        master.title("提示词格式转换工具 (v2.0)")
        master.geometry("500x200")

        self.input_filepath = None

        self.file_label = tk.Label(master, text="尚未选择文件", fg="grey", wraplength=480)
        self.file_label.pack(pady=(20, 5))

        self.select_button = tk.Button(master, text="选择 JSON 文件", command=self.select_file)
        self.select_button.pack(pady=5)

        self.convert_button = tk.Button(master, text="开始转换", command=self.convert_and_save, state=tk.DISABLED)
        self.convert_button.pack(pady=10)

        self.status_var = tk.StringVar()
        self.status_var.set("请先选择一个要转换的文件。")
        self.status_label = tk.Label(master, textvariable=self.status_var, fg="blue")
        self.status_label.pack(side=tk.BOTTOM, fill=tk.X, padx=10, pady=5)

    def select_file(self):
        filepath = filedialog.askopenfilename(
            title="请选择源JSON文件",
            filetypes=(("JSON 文件", "*.json"), ("所有文件", "*.*"))
        )
        if filepath:
            self.input_filepath = filepath
            filename = os.path.basename(filepath)
            self.file_label.config(text=f"已选择: {filename}", fg="black")
            self.status_var.set("文件已就绪，可以开始转换。")
            self.convert_button.config(state=tk.NORMAL)
        else:
            if not self.input_filepath:
                self.status_var.set("操作已取消。请选择一个文件。")


    def convert_and_save(self):
        if not self.input_filepath:
            messagebox.showerror("错误", "请先选择一个文件！")
            return

        try:
            self.status_var.set("正在读取和解析文件...")
            self.master.update_idletasks()
            with open(self.input_filepath, 'r', encoding='utf-8') as f:
                data = json.load(f)
        except Exception as e:
            messagebox.showerror("文件读取错误", f"无法读取或解析文件：\n{e}")
            self.status_var.set("错误：文件格式无效或无法读取。")
            return

        if 'prompts' not in data or not isinstance(data['prompts'], list):
            messagebox.showerror("格式错误", "JSON文件中未找到 'prompts' 列表或其格式不正确。")
            self.status_var.set("错误：JSON数据结构不符合要求。")
            return

        try:
            source_prompts = data['prompts']
            self.status_var.set(f"正在转换 {len(source_prompts)} 个源提示词...")
            self.master.update_idletasks()

            # ---【核心改动】---
            # 因为一个源提示词可能生成多个符文，所以需要一个循环和 extend
            target_runes = []
            for p in source_prompts:
                # convert_prompt_format 返回一个列表，我们将其所有元素添加到目标列表中
                target_runes.extend(convert_prompt_format(p))

        except Exception as e:
            messagebox.showerror("转换失败", f"在转换过程中发生错误：\n{e}")
            self.status_var.set("错误：转换过程中断。")
            return

        base, ext = os.path.splitext(self.input_filepath)
        default_output_name = f"{os.path.basename(base)}_converted.json"

        output_filepath = filedialog.asksaveasfilename(
            title="保存转换后的文件",
            initialfile=default_output_name,
            defaultextension=".json",
            filetypes=(("JSON 文件", "*.json"), ("所有文件", "*.*"))
        )

        if not output_filepath:
            self.status_var.set("保存操作已取消。")
            return

        try:
            self.status_var.set(f"正在写入 {len(target_runes)} 个新符文配置...")
            self.master.update_idletasks()
            with open(output_filepath, 'w', encoding='utf-8') as f:
                # 注意，现在我们转储的是 target_runes
                json.dump(target_runes, f, indent=2, ensure_ascii=False)

            messagebox.showinfo("成功", f"转换成功！\n文件已保存至：\n{output_filepath}")
            self.status_var.set("转换成功！可以继续选择其他文件。")

        except Exception as e:
            messagebox.showerror("保存失败", f"写入文件时发生错误：\n{e}")
            self.status_var.set("错误：写入文件失败。")


if __name__ == '__main__':
    root = tk.Tk()
    app = PromptConverterApp(root)
    root.mainloop()