import { defineComponent } from 'vue';
import { RouterView } from 'vue-router';
import { withSaveGameRoot } from '@yaesandbox-frontend/shared-feature/player-save';
import { createAndProvideEraLiteGameSaveService } from '#/saves/useEraLiteSaveStore';

// 创建一个只包含 <router-view> 的简单功能组件
const AppContentView = defineComponent(() => () => <RouterView />);

// 使用 HOC 包装这个简单的 <router-view> 组件
export default withSaveGameRoot(AppContentView, {
    createSaveService: createAndProvideEraLiteGameSaveService,
    appTitle: 'Era-Lite',
});