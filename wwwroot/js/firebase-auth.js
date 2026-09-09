// Firebase Authentication JS Bridge for Google Sign-In
(function () {
    const firebaseConfig = {
        apiKey: "AIzaSyD2oXJtBsr2uw5HcU2q6z9-u8moDn4JAeY",
        authDomain: "beautysalonsuite.firebaseapp.com",
        projectId: "beautysalonsuite"
    };

    let isInitialized = false;

    function ensureFirebase() {
        if (!isInitialized && typeof firebase !== 'undefined') {
            if (!firebase.apps.length) {
                firebase.initializeApp(firebaseConfig);
            }
            isInitialized = true;
        }
    }

    window.salonFirebaseAuth = {
        signInWithGoogle: async function () {
            try {
                ensureFirebase();

                if (typeof firebase === 'undefined' || !firebase.auth) {
                    return {
                        success: false,
                        errorMessage: "Firebase Authentication SDK failed to load. Please check your internet connection."
                    };
                }

                const provider = new firebase.auth.GoogleAuthProvider();
                provider.setCustomParameters({
                    prompt: 'select_account'
                });

                const result = await firebase.auth().signInWithPopup(provider);
                const user = result.user;
                const idToken = await user.getIdToken();

                return {
                    success: true,
                    email: user.email || "",
                    displayName: user.displayName || user.email?.split('@')[0] || "Client",
                    photoUrl: user.photoURL || "",
                    idToken: idToken,
                    uid: user.uid,
                    phoneNumber: user.phoneNumber || ""
                };
            } catch (error) {
                console.error("Google Sign-In Error:", error);

                let errorMsg = error.message || "Google Sign-In failed.";
                if (error.code === 'auth/popup-closed-by-user') {
                    errorMsg = "Sign-in popup was closed before completing.";
                } else if (error.code === 'auth/popup-blocked') {
                    errorMsg = "Popup was blocked by the browser. Please allow popups for this site.";
                } else if (error.code === 'auth/cancelled-popup-request') {
                    errorMsg = "Sign-in was cancelled.";
                } else if (error.code === 'auth/unauthorized-domain') {
                    errorMsg = "This domain is not authorized in Firebase Console (Authentication > Settings > Authorized domains).";
                }

                return {
                    success: false,
                    errorMessage: errorMsg
                };
            }
        }
    };
})();
