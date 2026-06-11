window.chiTieuLocation = {
    getCurrentPosition: function () {
        return new Promise(function (resolve) {
            if (!navigator.geolocation) {
                resolve({
                    ok: false,
                    error: "Trinh duyet khong ho tro lay vi tri."
                });
                return;
            }

            navigator.geolocation.getCurrentPosition(
                function (position) {
                    resolve({
                        ok: true,
                        latitude: position.coords.latitude,
                        longitude: position.coords.longitude,
                        accuracy: position.coords.accuracy
                    });
                },
                function (error) {
                    var message = "Khong lay duoc vi tri.";
                    if (error.code === error.PERMISSION_DENIED) {
                        message = "Ban chua cho phep trinh duyet truy cap vi tri.";
                    } else if (error.code === error.POSITION_UNAVAILABLE) {
                        message = "Thiet bi chua co vi tri kha dung.";
                    } else if (error.code === error.TIMEOUT) {
                        message = "Lay vi tri qua lau, thu lai khi tin hieu on hon.";
                    }

                    resolve({
                        ok: false,
                        error: message
                    });
                },
                {
                    enableHighAccuracy: true,
                    timeout: 12000,
                    maximumAge: 60000
                });
        });
    }
};
