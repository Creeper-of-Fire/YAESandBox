import { withSaveGameRoot } from '@yaesandbox-frontend/shared-feature/player-save';
import ActiveSessionView from '#/views/ActiveSessionView.vue';
import { createAndProvideEraMapGameSaveService } from '#/saves/useEraMapSaveStore';

export default withSaveGameRoot(ActiveSessionView, {
    createSaveService: createAndProvideEraMapGameSaveService,
    appTitle: 'Era-Map',
});