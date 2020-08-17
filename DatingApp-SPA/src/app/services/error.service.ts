import { Injectable } from "@angular/core";
import {
	HttpInterceptor,
	HttpEvent,
	HttpHandler,
	HttpRequest,
	HttpErrorResponse,
	HTTP_INTERCEPTORS
} from "@angular/common/http";
import { Observable, throwError } from "rxjs";
import { catchError } from "rxjs/operators";

@Injectable({
	providedIn: "root"
})
export default class ErrorService implements HttpInterceptor {
	intercept(
		req: HttpRequest<any>,
		next: HttpHandler
	): Observable<HttpEvent<any>> {
		return next.handle(req).pipe(
			catchError(error => {
				if (error.status === 401) return throwError(error.statusText);

				if (error instanceof HttpErrorResponse) {
					const applicationError = error.headers.get("Application-Error");

					if (applicationError) {
						console.error(applicationError);
						return throwError(applicationError);
					}

					const serverError = error.error;
					let modalStateErrors = "";

					if (serverError.errors && typeof serverError.errors === "object") {
						for (const key in serverError.errors) {
							if (!Object.prototype.hasOwnProperty.call(serverError.errors, key)) continue;
							if (serverError.errors[key]) modalStateErrors += serverError.errors[key] + "\n";
						}
					}

					return throwError(modalStateErrors || serverError || "Server Error.");
				}

				return throwError("Unknown Error.");
			})
		);
	}
}

export const ErrorInterceptorProvider = {
	provide: HTTP_INTERCEPTORS,
	useClass: ErrorService,
	multi: true
};
