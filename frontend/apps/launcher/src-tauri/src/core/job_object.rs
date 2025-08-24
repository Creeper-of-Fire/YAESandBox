// src-tauri/src/core/job_object.rs

#![cfg(windows)] // 确保此文件只在 Windows 上编译

use windows::Win32::Foundation::{CloseHandle, FALSE, HANDLE};
use windows::Win32::System::JobObjects::{
    AssignProcessToJobObject, CreateJobObjectW, SetInformationJobObject,
    JobObjectExtendedLimitInformation, JOBOBJECT_EXTENDED_LIMIT_INFORMATION,
    JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE,
};
use windows::Win32::System::Threading::{OpenProcess, PROCESS_SET_QUOTA, PROCESS_TERMINATE};

use std::mem;

// 一个安全的 JobObject 封装
pub struct JobObject {
    handle: HANDLE,
}

unsafe impl Sync for JobObject {}

unsafe impl Send for JobObject {}

impl JobObject {
    /// 创建一个新的 Job Object，并配置它在句柄关闭时终止所有关联进程
    pub fn new() -> Result<Self, String> {
        unsafe {
            // 1. 创建 Job Object 内核对象
            let handle = CreateJobObjectW(None, None)
                .map_err(|e| format!("Failed to create Job Object: {}", e))?;

            // 2. 配置 Job Object 的行为
            let mut limit_info = JOBOBJECT_EXTENDED_LIMIT_INFORMATION::default();
            limit_info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

            let info_size = mem::size_of::<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>() as u32;

            SetInformationJobObject(
                handle,
                JobObjectExtendedLimitInformation,
                &limit_info as *const _ as *const std::ffi::c_void,
                info_size,
            )
                .map_err(|e| format!("Failed to set Job Object information: {}", e))?;

            Ok(Self { handle })
        }
    }

    /// 通过进程 ID (PID) 将一个进程分配给这个 Job Object
    pub fn assign_process_by_pid(&self, pid: u32) -> Result<(), String> {
        unsafe {
            // 2. 通过 PID 打开进程，获取一个拥有特定权限的句柄
            // AssignProcessToJobObject 需要 PROCESS_SET_QUOTA 和 PROCESS_TERMINATE 权限
            let process_handle = OpenProcess(PROCESS_SET_QUOTA | PROCESS_TERMINATE, bool::from(FALSE), pid)
                .map_err(|e| format!("Failed to open process with PID {}: {}", pid, e))?;

            // 3. 将获取到的句柄分配给 Job Object
            let result = AssignProcessToJobObject(self.handle, process_handle);

            // 4. 无论成功与否，都关闭我们刚刚打开的句柄，避免句柄泄漏
            let _ = CloseHandle(process_handle);

            result.map_err(|e| format!("Failed to assign process to Job Object: {}", e))
        }
    }
}

// 实现 Drop trait，确保当 JobObject 实例离开作用域时，
// 其内核句柄会被自动关闭，从而触发 KILL_ON_JOB_CLOSE
impl Drop for JobObject {
    fn drop(&mut self) {
        unsafe {
            let _ = CloseHandle(self.handle);
        }
    }
}