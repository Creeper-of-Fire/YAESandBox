﻿using YAESandBox.Workflow.Config;

namespace YAESandBox.Workflow.DebugDto;

/// <inheritdoc />
public interface IStepProcessorDebugDto : IDebugDto
{
    /// <summary>
    /// 下属的 <see cref="AbstractModuleConfig{T}"/> 的Debug信息
    /// </summary>
    IList<IModuleProcessorDebugDto> ModuleProcessorDebugDtos { get; }
}