import {inject} from 'vue';
import {TokenResolverKey} from '@yaesandbox-frontend/core-services/injectKeys';
import {OpenAPI as WorkflowApiClient} from '@/types/generated/workflow-config-api-client';
import {OpenAPI as AiConfigApiClient} from '@/types/generated/ai-config-api-client';

export function configureApiClients()
{
    // 注入 resolver
    const tokenResolver = inject(TokenResolverKey);

    if (!tokenResolver)
    {
        throw new Error('未注入 TokenResolver');
    }

    // 【类型安全】
    // 因为我们在 core-services 中定义的 TokenResolver 类型与生成代码的类型兼容，
    // 所以这里的赋值是类型安全的。
    WorkflowApiClient.TOKEN = tokenResolver;
    AiConfigApiClient.TOKEN = tokenResolver;

    console.log('已配置 Workbench API 客户端的 TokenResolver');
}