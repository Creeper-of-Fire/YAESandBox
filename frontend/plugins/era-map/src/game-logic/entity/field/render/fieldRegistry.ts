export const fieldRegistry = {
    'light_level': {
        // 定义渲染方式，颜色映射等
        renderer: 'HeatmapFieldRenderer', // 指向一个渲染策略
        colorMap: ['rgba(0,0,0,1)', 'rgba(0,0,0,0)'],
    },
    // 'temperature': { ... }
};