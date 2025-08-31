import type { Directive } from 'vue';

const directive: Directive<HTMLElement, () => void> = {
    mounted(el, binding) {
        const observer = new IntersectionObserver(
            (entries) => {
                if (entries[0].isIntersecting) {
                    // 元素可见时，调用指令绑定的函数
                    binding.value();
                    observer.unobserve(el);
                }
            },
            { rootMargin: '200px 0px' }
        );
        observer.observe(el);
        // 可以在 el 上存储 observer 实例以便在 unmounted 时清理
        (el as any)._lazyObserver = observer;
    },
    unmounted(el) {
        if ((el as any)._lazyObserver) {
            (el as any)._lazyObserver.disconnect();
        }
    },
};

export default directive;