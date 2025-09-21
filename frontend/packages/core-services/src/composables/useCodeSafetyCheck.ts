import {h, ref, type VNode} from 'vue';
import {NCheckbox, NList, NListItem, NThing, useDialog} from 'naive-ui';
import {useScopedStorage} from './useScopedStorage';

// 定义检测器接口和注册表
interface ObfuscationPattern
{
    name: string;
    description: string;
    detector: (code: string) => boolean;
}

const patterns: ObfuscationPattern[] = [
    {
        name: '动态代码执行',
        description: '检测到使用 eval(), new Function(), 或将字符串传递给 setTimeout/setInterval。这些函数可以执行任意代码，可能存在安全风险。',
        detector: (code) => /\b(eval|Function)\s*\(|setTimeout\s*\(\s*["']|setInterval\s*\(\s*["']/.test(code),
    },
    {
        name: 'JSFuck / JJencode 风格混淆',
        description: '代码中包含大量仅由 `[]()!+` 等少量字符组成的超长序列，这是高度混淆的迹象。',
        detector: (code) => /[()\[\]!+]{20,}/.test(code), // 检查是否有超过20个连续的此类字符
    },
    {
        name: '代码过度压缩',
        description: '代码包含超过500个字符的超长单行，或极低的空格/换行比例(低于5%)，这使得代码难以阅读和审计，可能包含无法察觉的风险代码。',
        detector: (code) =>
        {
            // 检查超长行
            if (code.split('\n').some(line => line.length > 500))
            {
                return true;
            }
            // 检查空格比例
            const nonWhitespace = code.replace(/\s/g, '').length;
            const total = code.length;
            if (total > 200 && (total - nonWhitespace) / total < 0.05)
            { // 空白字符少于5%
                return true;
            }
            return false;
        },
    },
];

/**
 * 渲染一个包含安全建议的 VNode 列表。
 * @returns {VNode}
 */
const renderSuggestions = (): VNode =>
{
    return h('div', {style: {marginTop: '16px'}}, [
        h('p', {style: {fontWeight: 'bold'}}, '如果您不确定代码的安全性，可以尝试以下操作：'),
        h('ul', {style: {paddingLeft: '20px', marginTop: '8px', marginBottom: 0}}, [
            h('li', null, [
                '将代码粘贴到 ',
                h('strong', null, 'AI 助手'),
                '中，询问其功能和潜在风险。'
            ]),
            h('li', null, '在搜索引擎中搜索代码片段，查看是否有关于它的讨论或警告。'),
            h('li', null, '如果您懂代码，请仔细审查。如果不懂，请不要运行来源不明的代码。'),
        ])
    ]);
};


/**
 * 提供一个函数，用于在执行代码前进行安全检查。
 */
export function useCodeSafetyCheck()
{
    const dialog = useDialog();
    const dismissThirdPartyWarning = useScopedStorage('dismissThirdPartyWarning', false, localStorage);

    /**
     * 检查代码是否存在潜在的安全风险。
     * @param code 要检查的源代码字符串。
     * @returns Promise<boolean> - 如果用户同意继续，则解析为 true，否则为 false。
     */
    const checkCodeSafety = (code: string): Promise<boolean> =>
    {
        return new Promise((resolve) =>
        {
            // --- 1. 强制性混淆检测 ---
            const detectedPatterns = patterns.filter(p => p.detector(code));

            if (detectedPatterns.length > 0)
            {
                dialog.error({
                    title: '潜在的恶意代码警告',
                    closable: false,
                    maskClosable: false,
                    content: () => h('div', null, [
                        h('p', null, '检测到以下可能用于隐藏恶意行为的代码模式。我们强烈建议您不要运行来源不可信的代码。'),
                        h(NList, {bordered: true, style: {marginTop: '12px'}}, {
                            default: () => detectedPatterns.map(p =>
                                h(NListItem, null, {
                                    default: () => h(NThing, {title: p.name, description: p.description})
                                })
                            )
                        }),
                        renderSuggestions(),
                    ]),
                    positiveText: '我了解风险，仍然继续',
                    negativeText: '取消',
                    onPositiveClick: () => resolve(true),
                    onNegativeClick: () => resolve(false),
                });
                return; // 结束函数，等待用户决策
            }

            // --- 2. 可选的第三方代码免责声明 ---
            if (dismissThirdPartyWarning.value)
            {
                resolve(true); // 用户已选择不再提醒
                return;
            }

            const checkboxState = ref(false);
            dialog.warning({
                title: '运行第三方代码',
                content: () => h('div', null, [
                    h('p', null, '您正在运行/使用自定义代码。请确保您信任代码的来源，因为恶意代码可能会损害您的数据或执行非预期操作。'),
                    renderSuggestions(),
                    h(NCheckbox,
                        {
                            style: {marginTop: '16px'},
                            checked: checkboxState.value,
                            'onUpdate:checked': (val) => checkboxState.value = val,
                        },
                        {default: () => '不再提醒我'}
                    ),
                ]),
                positiveText: '我已知晓，继续',
                negativeText: '取消',
                onPositiveClick: () =>
                {
                    if (checkboxState.value)
                    {
                        dismissThirdPartyWarning.value = true;
                    }
                    resolve(true);
                },
                onNegativeClick: () => resolve(false),
            });
        });
    };

    return {
        checkCodeSafety,
    };
}