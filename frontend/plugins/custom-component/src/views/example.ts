export const exampleCode = `
// 用户编写的 char-status-bar.tsx

// --- 辅助函数：一个简单的 XML 解析器 ---
// 在真实场景中，可能会使用更健壮的库，但这里为了演示，手写一个。
function parseStatusContent(rawContent) {
    const parser = new DOMParser();
    const doc = parser.parseFromString(\`<root>\${rawContent}</root>\`, "application/xml");
    const data = {
        hp: { current: 0, max: 100 },
        mp: { current: 0, max: 100 },
        effects: []
    };
    
    const hpNode = doc.querySelector('hp');
    if (hpNode) {
        data.hp.current = parseInt(hpNode.getAttribute('current') || '0', 10);
        data.hp.max = parseInt(hpNode.getAttribute('max') || '100', 10);
    }
    
    const mpNode = doc.querySelector('mp');
    if (mpNode) {
        data.mp.current = parseInt(mpNode.getAttribute('current') || '0', 10);
        data.mp.max = parseInt(mpNode.getAttribute('max') || '60', 10);
    }
    
    doc.querySelectorAll('status-effect').forEach(node => {
        data.effects.push({
            type: node.getAttribute('type'),
            name: node.textContent
        });
    });
    
    console.log(data)
    
    return data;
}


// --- 组件定义 ---
const CharacterStatusBar = {
    // 1. 声明接收来自 <char-status-bar ...> 标签的 props
    //    和来自 ContentRenderer 的 rawContent
    props: {
        name: String,
        level: String,
        class: String,
        rawContent: String, // 由 ContentRenderer 自动传入
    },

    // 2. 声明要触发的事件
    emits: ['hp-change'],

    // 3. setup 函数是所有逻辑的核心
    setup(props, { slots, emit }) {

        // 4. 解析 rawContent，并将其作为初始的内部响应式状态
        //    我们使用 shallowRef 来存储整个状态对象
        const statusData = Vue.shallowRef(parseStatusContent(props.rawContent));

        // 创建一个可写的 hp 计算属性，用于 v-model
        const currentHp = Vue.computed({
            get: () => statusData.value.hp.current,
            set: (newValue) => {
                // 更新内部状态
                const newData = { ...statusData.value, hp: { ...statusData.value.hp, current: newValue }};
                statusData.value = newData;
                // 触发 emit 事件
                emit('hp-change', newValue);
            }
        });

        // 计算百分比
        const hpPercent = Vue.computed(() => (statusData.value.hp.current / statusData.value.hp.max) * 100);
        const mpPercent = Vue.computed(() => (statusData.value.mp.current / statusData.value.mp.max) * 100);

        // 默认插槽的内容（来自 ContentRenderer）
        // 我们用一个 ref 来控制折叠面板的显示
        const showRawContent = Vue.ref(false);
        
        // 创建一个 computed 属性来适配 NCollapse 的 v-model
        const expandedNames = Vue.computed({
            // Getter: 当 NCollapse 需要知道哪些项展开时调用
            get() {
                // 如果我们的布尔状态是 true，就返回包含面板 name 的数组
                return showRawContent.value ? ['rawContentPanel'] : [];
            },
            // Setter: 当用户点击 NCollapseItem 改变状态时调用
            set(newArray) {
                // 如果新数组包含我们的面板 name，就将布尔状态设为 true
                showRawContent.value = newArray.includes('rawContentPanel');
            }
        });

        // --- 渲染函数 ---
        return () => (
            <NCard title={\`\${props.name} - Lvl \${props.level} \${props.class}\`}>
                {/* 渲染 HP 和 MP 进度条 */}
                <div>
                    <strong>HP:</strong> {statusData.value.hp.current} / {statusData.value.hp.max}
                    <NProgress type="line" status="error" percentage={hpPercent.value} />
                </div>
                <div style={{ marginTop: '12px' }}>
                    <strong>MP:</strong> {statusData.value.mp.current} / {statusData.value.mp.max}
                    <NProgress type="line" status="info" percentage={mpPercent.value} />
                </div>
        
                {/* 渲染状态效果 */}
                <div style={{ marginTop: '16px', display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
                {statusData.value.effects.map(effect => (
                    <NTag type={effect.type === 'buff' ? 'success' : 'warning'}>
                        {effect.name}
                    </NTag>
                ))}
                </div>
        
                {/* HP 控制滑块，使用 v-model 与 currentHp 双向绑定 */}
                <div style={{ marginTop: '20px' }}>
                <NSlider v-model={[currentHp.value]} max={statusData.value.hp.max} />
                </div>
        
                {/* 渲染来自 SLOT 的原始数据，并放入一个折叠组件中 */}
                <NCollapse style={{ marginTop: '20px' }} v-model={[expandedNames.value]}>
                    <NCollapseItem title="View Raw Content (from Slot)" name="rawContentPanel">
                    <pre style={{ backgroundColor: '#f5f5f5', padding: '10px', borderRadius: '4px' }}>
                        <code>{slots.default ? slots.default() : 'No slot content'}</code>
                    </pre>
                    </NCollapseItem>
                </NCollapse>
            </NCard>
    );
    }
};

// 确保组件对象是最后一个表达式
CharacterStatusBar
`;

export const exampleName = 'char-status-bar';

export const exampleContent = `
<char-status-bar name="Alice" level="15" class="Warrior">
  <hp current="85" max="100" />
  <mp current="40" max="60" />
  <status-effect type="buff">Blessing</status-effect>
  <status-effect type="debuff">Poison</status-effect>
</char-status-bar>
`