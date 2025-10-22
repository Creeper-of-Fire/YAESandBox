import type {AbstractRuneConfig, AnyConfigObject, TuumConfig, WorkflowConfig} from "../types";

// 定义一个可辨识联合类型（Discriminated Union Type）
// 这个类型精确地描述了函数的返回值结构
export type DiscriminatedConfig =
    | { type: 'workflow'; config: WorkflowConfig }
    | { type: 'tuum'; config: TuumConfig }
    | { type: 'rune'; config: AbstractRuneConfig };

/**
 * 根据对象的结构特征，判断并返回其具体的配置类型。
 * @param config - 待检查的任意配置对象。
 */
export function getConfigObjectType(config: AnyConfigObject): DiscriminatedConfig
{
    // 因为每个类型的关键属性是唯一的，所以判断顺序无关紧要。
    if (isWorkflow(config))
    {
        return {type: 'workflow' as const, config};
    }
    else if (isTuum(config))
    {
        return {type: 'tuum' as const, config};
    }
    else
    {
        return {type: 'rune' as const, config};
    }
}

/**
 * 定义一个更具体的类型，表示一个包含有效 innerTuum 的符文。
 * 这是一个交叉类型，它要求对象同时满足 AbstractRuneConfig 和拥有 innerTuum: TuumConfig 属性。
 */
export type RuneWithInnerTuum = AbstractRuneConfig & {
    innerTuum: TuumConfig
};

export function isRuneWithInnerTuum(config: AnyConfigObject): config is RuneWithInnerTuum
{
    // 1. 首先，它必须是一个合法的符文。
    // 2. 然后，它必须包含 `innerTuum` 属性。
    // 3. 最关键的是，`innerTuum` 属性的值本身必须是一个合法的 TuumConfig。
    return (
        isRune(config) &&
        'innerTuum' in config &&
        isTuum(<WorkflowConfig | TuumConfig | AbstractRuneConfig>(config as { innerTuum: unknown }).innerTuum)
    );
}

/**
 * 类型守卫：检查一个配置对象是否为 WorkflowConfig。
 * @param config - 待检查的任意配置对象。
 */
export function isWorkflow(config: AnyConfigObject): config is WorkflowConfig
{
    return 'tuums' in config && 'workflowInputs' in config;
}

/**
 * 类型守卫：检查一个配置对象是否为 TuumConfig。
 * @param config - 待检查的任意配置对象。
 */
export function isTuum(config: AnyConfigObject): config is TuumConfig
{
    return 'runes' in config;
}

/**
 * 类型守卫：检查一个配置对象是否为 AbstractRuneConfig 的派生类。
 * @param config - 待检查的任意配置对象。
 */
export function isRune(config: AnyConfigObject): config is AbstractRuneConfig
{
    return 'runeType' in config && !isWorkflow(config) && !isTuum(config);
}
