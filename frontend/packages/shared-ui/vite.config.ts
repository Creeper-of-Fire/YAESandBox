import {defineConfig} from 'vite';
import {createMonorepoViteConfig} from '../../vite.config.shared';

export default defineConfig(
    createMonorepoViteConfig({
        packageDir: __dirname,
        type: 'plugin',
        plugins: [],
    })
);