type particleConfig = {
    colorRange:string[];
    sizeRange:number[];
    placement:string;
}

export const particleRegistry:Record<string, particleConfig> = {
    'grime': {
        colorRange: ['#6B8E23', '#90a239'],
        sizeRange: [1, 3],
        placement: "random_within_cell"
    },
    'torch_embers': {
        colorRange: ['#e86d38', '#f39712'],
        sizeRange: [1, 3],
        placement: "random_within_cell"
    },
};