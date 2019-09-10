import {MiddlewareConsumer, Module, RequestMethod} from '@nestjs/common';
import {AppController} from './controllers/app.controller';
import {AppService} from './providers/app.service';
import {CardsController} from './controllers/cards.controller';
import {UserService} from './providers/user.service';
import {UsersController} from './controllers/users.controller';
import {LoginMiddleware} from './middlewares/login.middleware';
import {ApiMiddleware} from './middlewares/api.middleware';
import {EncryptionService} from './providers/encryption.service';
import {RequestService} from './providers/request.service';

@Module({
    imports: [],
    controllers: [AppController, CardsController, UsersController],
    providers: [AppService, UserService, EncryptionService, RequestService],
})
export class AppModule {
    configure(consumer: MiddlewareConsumer) {
        consumer
            .apply(LoginMiddleware)
            .forRoutes(UsersController);

        consumer
            .apply(ApiMiddleware)
            .forRoutes({ path: 'cards/*', method: RequestMethod.ALL });
    }
}